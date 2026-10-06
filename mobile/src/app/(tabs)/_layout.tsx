import { MaterialCommunityIcons } from '@expo/vector-icons';
import { Tabs } from 'expo-router';
import type { ColorValue } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Guard } from '../../components/Guard';
import { theme } from '../../lib/theme';

type IconName = keyof typeof MaterialCommunityIcons.glyphMap;
const icon = (name: IconName) => ({ color, size }: { color: ColorValue; size: number }) => <MaterialCommunityIcons name={name} color={color as string} size={size} />;

export default function TabsLayout() {
  const { t } = useTranslation();
  return (
    <Guard>
      <Tabs
        screenOptions={{
          headerStyle: { backgroundColor: theme.colors.primary },
          headerTintColor: '#fff',
          tabBarActiveTintColor: theme.colors.primary,
        }}
      >
        <Tabs.Screen name="index" options={{ title: t('nav.dashboard'), tabBarIcon: icon('view-dashboard-outline') }} />
        <Tabs.Screen name="invoices" options={{ title: t('nav.invoices'), tabBarIcon: icon('file-document-outline') }} />
        <Tabs.Screen name="quotations" options={{ title: t('nav.quotations'), tabBarIcon: icon('file-document-edit-outline') }} />
        <Tabs.Screen name="clients" options={{ title: t('nav.clients'), tabBarIcon: icon('account-group-outline') }} />
        <Tabs.Screen name="more" options={{ title: t('mobile.more'), tabBarIcon: icon('dots-horizontal') }} />
      </Tabs>
    </Guard>
  );
}
