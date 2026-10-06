import { Button, Result, Spin } from 'antd';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { useAuth } from './AuthContext';

export function RequireAuth({ children }: { children: ReactNode }) {
  const { user, initializing } = useAuth();
  const location = useLocation();
  if (initializing) {
    return (
      <div style={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
        <Spin size="large" />
      </div>
    );
  }
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  return <>{children}</>;
}

/** Admin-only pages: Cashiers see a 403 page (the API enforces the same rules). */
export function AdminOnly({ children }: { children: ReactNode }) {
  const { isAdmin } = useAuth();
  return isAdmin ? <>{children}</> : <Forbidden />;
}

export function Forbidden() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <Result
      status="403"
      title="403"
      subTitle={t('errors.forbidden')}
      extra={<Button onClick={() => navigate('/dashboard')}>{t('errors.goHome')}</Button>}
      data-testid="forbidden"
    />
  );
}

export function NotFound() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return <Result status="404" title="404" subTitle={t('errors.notFound')} extra={<Button onClick={() => navigate('/dashboard')}>{t('errors.goHome')}</Button>} />;
}
