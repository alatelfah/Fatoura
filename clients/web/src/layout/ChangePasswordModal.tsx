import { App, Form, Input, Modal } from 'antd';
import { useTranslation } from 'react-i18next';
import { fetchClient } from '../api/client';
import { applyProblem } from '../components/problems';

export function ChangePasswordModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [form] = Form.useForm<{ currentPassword: string; newPassword: string }>();
  const submit = async () => {
    const values = await form.validateFields().catch(() => null);
    if (!values) return;
    const { error } = await fetchClient.POST('/api/auth/change-password', { body: values });
    if (error) {
      message.error(applyProblem(error, form));
      return;
    }
    message.success(t('auth.passwordChanged'));
    form.resetFields();
    onClose();
  };
  return (
    <Modal open={open} title={t('nav.changePassword')} onOk={submit} onCancel={onClose} okText={t('common.save')} cancelText={t('common.cancel')} destroyOnHidden>
      <Form form={form} layout="vertical">
        <Form.Item name="currentPassword" label={t('auth.currentPassword')} rules={[{ required: true, message: t('common.required') }]}>
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Form.Item name="newPassword" label={t('auth.newPassword')} extra={t('auth.passwordRule')} rules={[{ required: true, min: 8, message: t('auth.passwordRule') }]}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
      </Form>
    </Modal>
  );
}
