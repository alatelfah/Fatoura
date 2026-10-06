import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { App, Button, Checkbox, Drawer, Form, Input, Popconfirm, Switch, Table, Tag } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { isValidTrn, normalizeTrn } from '@fatoura/shared';
import { $api, fetchClient, type Schemas } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { Ltr } from '../../components/Ltr';
import { PageHeader } from '../../components/PageHeader';
import { applyProblem } from '../../components/problems';

type Kind = 'clients' | 'suppliers';
type Contact = Schemas['ClientDto'] | Schemas['SupplierDto'];

export function ClientsPage() {
  return <ContactsPage kind="clients" />;
}

export function SuppliersPage() {
  return <ContactsPage kind="suppliers" />;
}

/** TRN is optional for contacts but must be valid when entered (15 digits starting with 10). */
export function trnRule(message: string) {
  return {
    validator: async (_: unknown, value?: string) => {
      if (value && !isValidTrn(value)) throw new Error(message);
    },
  };
}

function ContactsPage({ kind }: { kind: Kind }) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [params, setParams] = useSearchParams();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<Contact | 'new' | null>(null);
  const [form] = Form.useForm();
  const path = kind === 'clients' ? '/api/clients' : '/api/suppliers';
  const list = $api.useQuery('get', path, { params: { query: { search: search || undefined, page, pageSize: 20, includeInactive } } });
  const canEdit = isAdmin; // cashiers may add clients but not edit them (BRD §2)

  useEffect(() => {
    if (params.get('new') === '1') {
      setEditing('new');
      params.delete('new');
      setParams(params, { replace: true });
    }
  }, [params, setParams]);

  useEffect(() => {
    if (editing === 'new') form.setFieldsValue({ name: '', phone: '', email: '', address: '', trn: '', isActive: true });
    else if (editing) form.setFieldsValue(editing);
  }, [editing, form]);

  const save = async () => {
    const values = await form.validateFields().catch(() => null);
    if (!values) return;
    const body = { ...values, trn: normalizeTrn(values.trn) };
    const { error } =
      editing === 'new'
        ? await fetchClient.POST(path, { body })
        : await fetchClient.PUT(`${path}/{id}` as '/api/clients/{id}', { params: { path: { id: (editing as Contact).id } }, body });
    if (error) {
      message.error(applyProblem(error, form));
      return;
    }
    message.success(t('common.saved'));
    setEditing(null);
    await queryClient.invalidateQueries({ queryKey: ['get', path] });
  };

  const remove = async (id: number) => {
    const { error } = await fetchClient.DELETE(`${path}/{id}` as '/api/clients/{id}', { params: { path: { id } } });
    if (error) message.error(applyProblem(error));
    else {
      message.success(t('common.deleted'));
      await queryClient.invalidateQueries({ queryKey: ['get', path] });
    }
  };

  const title = kind === 'clients' ? t('nav.clients') : t('nav.suppliers');
  return (
    <>
      <PageHeader
        title={title}
        extra={
          <>
            <Input.Search allowClear placeholder={t('common.search')} onSearch={(v) => { setSearch(v); setPage(1); }} style={{ width: 240 }} data-testid="contacts-search" />
            {isAdmin && <Checkbox checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)}>{t('common.showInactive')}</Checkbox>}
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')} data-testid="contact-new">
              {kind === 'clients' ? t('contact.newClient') : t('contact.newSupplier')}
            </Button>
          </>
        }
      />
      <Table
        rowKey="id"
        loading={list.isLoading}
        dataSource={list.data?.items}
        scroll={{ x: 800 }}
        pagination={{ current: page, pageSize: 20, total: list.data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: t('contact.name'), dataIndex: 'name', render: (v: string, r) => <>{v} {!r.isActive && <Tag>{t('common.inactive')}</Tag>}</> },
          { title: t('contact.phone'), dataIndex: 'phone', render: (v: string) => <Ltr>{v}</Ltr> },
          { title: t('contact.email'), dataIndex: 'email' },
          { title: t('contact.address'), dataIndex: 'address', ellipsis: true },
          { title: t('contact.trn'), dataIndex: 'trn', render: (v: string) => <Ltr>{v}</Ltr> },
          ...(canEdit
            ? [
                {
                  title: t('common.actions'),
                  key: 'actions',
                  width: 110,
                  render: (_: unknown, r: Contact) => (
                    <>
                      <Button type="text" icon={<EditOutlined />} onClick={() => setEditing(r)} aria-label={t('common.edit')} />
                      <Popconfirm title={t('contact.deleteConfirm')} onConfirm={() => remove(r.id)} okText={t('common.yes')} cancelText={t('common.no')}>
                        <Button type="text" danger icon={<DeleteOutlined />} aria-label={t('common.delete')} />
                      </Popconfirm>
                    </>
                  ),
                },
              ]
            : []),
        ]}
      />
      <Drawer
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? (kind === 'clients' ? t('contact.newClient') : t('contact.newSupplier')) : kind === 'clients' ? t('contact.editClient') : t('contact.editSupplier')}
        size={480}
        destroyOnHidden
        extra={<Button type="primary" onClick={save} data-testid="contact-save">{t('common.save')}</Button>}
      >
        <Form form={form} layout="vertical" onFinish={save}>
          <Form.Item name="name" label={kind === 'clients' ? t('contact.clientName') : t('contact.supplierName')} rules={[{ required: true, whitespace: true, message: t('common.required') }]}>
            <Input maxLength={200} data-testid="contact-name" />
          </Form.Item>
          <Form.Item name="phone" label={t('contact.phone')}>
            <Input maxLength={50} dir="ltr" />
          </Form.Item>
          <Form.Item name="email" label={t('contact.email')} rules={[{ type: 'email', message: t('common.invalidEmail') }]}>
            <Input maxLength={256} dir="ltr" />
          </Form.Item>
          <Form.Item name="address" label={t('contact.address')}>
            <Input.TextArea maxLength={500} autoSize={{ minRows: 2 }} />
          </Form.Item>
          <Form.Item name="trn" label={t('contact.trn')} extra={t('contact.trnHelp')} rules={[trnRule(t('contact.trnInvalid'))]}>
            <Input maxLength={20} dir="ltr" inputMode="numeric" data-testid="contact-trn" />
          </Form.Item>
          {editing !== 'new' && (
            <Form.Item name="isActive" label={t('common.active')} valuePropName="checked">
              <Switch />
            </Form.Item>
          )}
        </Form>
      </Drawer>
    </>
  );
}
