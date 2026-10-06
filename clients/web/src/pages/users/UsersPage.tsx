import { EditOutlined, KeyOutlined, PlusOutlined } from '@ant-design/icons';
import { App, Button, Form, Input, Modal, Select, Switch, Table, Tag } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { $api, fetchClient, type Schemas } from '../../api/client';
import { PageHeader } from '../../components/PageHeader';
import { applyProblem } from '../../components/problems';
import { formatDate } from '../../utils/format';

type User = Schemas['UserDto'];

export function UsersPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { data, isLoading, refetch } = $api.useQuery('get', '/api/users');
  const [editing, setEditing] = useState<User | 'new' | null>(null);
  const [resetting, setResetting] = useState<User | null>(null);
  const [form] = Form.useForm();
  const [resetForm] = Form.useForm();

  const open = (u: User | 'new') => {
    setEditing(u);
    form.setFieldsValue(u === 'new' ? { email: '', displayName: '', password: '', role: 'Cashier' } : { displayName: u.displayName, role: u.role, isActive: u.isActive });
  };

  const save = async () => {
    const v = await form.validateFields().catch(() => null);
    if (!v) return;
    const { error } = editing === 'new'
      ? await fetchClient.POST('/api/users', { body: v })
      : await fetchClient.PUT('/api/users/{id}', { params: { path: { id: (editing as User).id } }, body: v });
    if (error) return void message.error(applyProblem(error, form));
    message.success(t('common.saved'));
    setEditing(null);
    await refetch();
  };

  const reset = async () => {
    const v = await resetForm.validateFields().catch(() => null);
    if (!v) return;
    const { error } = await fetchClient.POST('/api/users/{id}/reset-password', { params: { path: { id: resetting!.id } }, body: v });
    if (error) return void message.error(applyProblem(error, resetForm));
    message.success(t('users.passwordReset'));
    setResetting(null);
    resetForm.resetFields();
  };

  const roleOptions = [{ value: 'Cashier', label: t('roles.Cashier') }, { value: 'Admin', label: t('roles.Admin') }];
  return (
    <>
      <PageHeader title={t('users.title')} extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => open('new')} data-testid="user-new">{t('users.new')}</Button>} />
      <Table
        rowKey="id"
        loading={isLoading}
        dataSource={data}
        pagination={false}
        scroll={{ x: 700 }}
        columns={[
          { title: t('users.displayName'), dataIndex: 'displayName' },
          { title: t('contact.email'), dataIndex: 'email' },
          { title: t('users.role'), dataIndex: 'role', render: (r: string) => <Tag color={r === 'Admin' ? 'gold' : 'blue'}>{t(`roles.${r}`)}</Tag> },
          { title: t('common.status'), dataIndex: 'isActive', render: (a: boolean) => (a ? <Tag color="green">{t('common.active')}</Tag> : <Tag>{t('common.inactive')}</Tag>) },
          { title: t('users.created'), dataIndex: 'createdAt', render: (v: string) => formatDate(v) },
          {
            title: t('common.actions'),
            key: 'a',
            width: 110,
            render: (_: unknown, u: User) => (
              <>
                <Button type="text" icon={<EditOutlined />} onClick={() => open(u)} aria-label={t('common.edit')} />
                <Button type="text" icon={<KeyOutlined />} onClick={() => setResetting(u)} title={t('users.resetPassword')} />
              </>
            ),
          },
        ]}
      />
      <Modal open={editing !== null} title={editing === 'new' ? t('users.new') : t('users.edit')} onOk={save} onCancel={() => setEditing(null)} okText={t('common.save')} cancelText={t('common.cancel')} okButtonProps={{ 'data-testid': 'user-save' } as never} destroyOnHidden>
        <Form form={form} layout="vertical">
          {editing === 'new' && (
            <Form.Item name="email" label={t('contact.email')} rules={[{ required: true, type: 'email', message: t('common.invalidEmail') }]}>
              <Input dir="ltr" data-testid="user-email" />
            </Form.Item>
          )}
          <Form.Item name="displayName" label={t('users.displayName')} rules={[{ required: true, whitespace: true, message: t('common.required') }]}>
            <Input maxLength={100} data-testid="user-name" />
          </Form.Item>
          {editing === 'new' && (
            <Form.Item name="password" label={t('auth.password')} extra={t('auth.passwordRule')} rules={[{ required: true, min: 8, message: t('auth.passwordRule') }]}>
              <Input.Password autoComplete="new-password" data-testid="user-password" />
            </Form.Item>
          )}
          <Form.Item name="role" label={t('users.role')}>
            <Select options={roleOptions} />
          </Form.Item>
          {editing !== 'new' && (
            <Form.Item name="isActive" label={t('common.active')} valuePropName="checked">
              <Switch />
            </Form.Item>
          )}
        </Form>
      </Modal>
      <Modal open={resetting !== null} title={`${t('users.resetPassword')} — ${resetting?.displayName ?? ''}`} onOk={reset} onCancel={() => setResetting(null)} okText={t('common.save')} cancelText={t('common.cancel')} destroyOnHidden>
        <Form form={resetForm} layout="vertical">
          <Form.Item name="newPassword" label={t('auth.newPassword')} extra={t('auth.passwordRule')} rules={[{ required: true, min: 8, message: t('auth.passwordRule') }]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
