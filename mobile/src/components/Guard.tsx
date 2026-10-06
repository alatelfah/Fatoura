import { Redirect } from 'expo-router';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Text } from 'react-native-paper';
import { useAuth } from '../lib/auth';
import { Loading, Screen } from './ui';

/** Redirects to the login screen when signed out; optionally restricts to Admins (the API enforces the same). */
export function Guard({ children, admin }: { children: ReactNode; admin?: boolean }) {
  const { t } = useTranslation();
  const { user, ready, isAdmin } = useAuth();
  if (!ready) return <Loading />;
  if (!user) return <Redirect href="/login" />;
  if (admin && !isAdmin) {
    return (
      <Screen>
        <Text>{t('errors.forbidden')}</Text>
      </Screen>
    );
  }
  return <>{children}</>;
}
