import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { List, Text } from 'react-native-paper';
import { LanguageToggle } from '../../components/LanguageToggle';
import { Screen } from '../../components/ui';
import { API_URL } from '../../lib/api';
import { useAuth } from '../../lib/auth';

export default function MoreScreen() {
  const { t } = useTranslation();
  const { user, isAdmin, logout } = useAuth();
  return (
    <Screen>
      <Text variant="titleMedium">{t('mobile.signedInAs', { name: user?.displayName })} · {t(`roles.${user?.role ?? 'Cashier'}`)}</Text>
      <List.Section>
        <List.Item title={t('nav.creditNotes')} left={(p) => <List.Icon {...p} icon="undo-variant" />} onPress={() => router.push('/credit-notes')} />
        <List.Item title={t('nav.items')} left={(p) => <List.Icon {...p} icon="package-variant" />} onPress={() => router.push('/items')} />
        <List.Item title={t('nav.reports')} left={(p) => <List.Icon {...p} icon="chart-bar" />} onPress={() => router.push('/reports')} />
        {isAdmin && <List.Item title={t('nav.purchases')} left={(p) => <List.Icon {...p} icon="cart-outline" />} onPress={() => router.push('/purchases')} />}
        {isAdmin && <List.Item title={t('nav.suppliers')} left={(p) => <List.Icon {...p} icon="store-outline" />} onPress={() => router.push('/suppliers')} />}
        {isAdmin && <List.Item title={t('nav.settings')} description={t('mobile.webOnly')} left={(p) => <List.Icon {...p} icon="cog-outline" />} disabled />}
      </List.Section>
      <LanguageToggle />
      <List.Item title={t('nav.logout')} titleStyle={{ color: '#c62828' }} left={(p) => <List.Icon {...p} icon="logout" color="#c62828" />} onPress={async () => { await logout(); router.replace('/login'); }} testID="logout" />
      <Text style={{ opacity: 0.5, fontSize: 12 }}>{t('mobile.serverUrl')}: {API_URL || 'same origin'}</Text>
    </Screen>
  );
}
