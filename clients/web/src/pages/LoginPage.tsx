import { LockOutlined, MailOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Flex, Form, Input, Typography } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { LanguageSwitch } from '../layout/LanguageSwitch';

export function LoginPage() {
  const { t } = useTranslation();
  const { user, login, sessionExpired, initializing } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const from = (location.state as { from?: string } | null)?.from ?? '/dashboard';

  if (!initializing && user) return <Navigate to={from} replace />;

  const onFinish = async (values: { email: string; password: string }) => {
    setLoading(true);
    setError(null);
    try {
      const result = await login(values.email, values.password);
      if (result === 'ok') navigate(from, { replace: true });
      else setError(result === 'locked' ? 'Account temporarily locked. Try again later.' : result === 'disabled' ? 'This account is disabled.' : t('auth.invalid'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <Card style={{ width: 380, maxWidth: '100%' }}>
        <Flex vertical align="center" gap={4} style={{ marginBottom: 24 }}>
          <img src="/api/settings/logo" alt="" style={{ maxHeight: 64, maxWidth: 220 }} onError={(e) => (e.currentTarget.style.display = 'none')} />
          <Typography.Title level={3} style={{ margin: 0 }}>
            {t('app.name')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('app.tagline')}</Typography.Text>
        </Flex>
        {sessionExpired && <Alert type="warning" title={t('auth.sessionExpired')} showIcon style={{ marginBottom: 16 }} />}
        {error && <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} data-testid="login-error" />}
        <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item name="email" label={t('auth.email')} rules={[{ required: true, type: 'email', message: t('common.invalidEmail') }]}>
            <Input prefix={<MailOutlined />} autoComplete="username" dir="ltr" data-testid="login-email" />
          </Form.Item>
          <Form.Item name="password" label={t('auth.password')} rules={[{ required: true, message: t('common.required') }]}>
            <Input.Password prefix={<LockOutlined />} autoComplete="current-password" data-testid="login-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={loading} data-testid="login-submit">
            {t('auth.signIn')}
          </Button>
        </Form>
        <Flex justify="center" style={{ marginTop: 16 }}>
          <LanguageSwitch />
        </Flex>
      </Card>
    </div>
  );
}
