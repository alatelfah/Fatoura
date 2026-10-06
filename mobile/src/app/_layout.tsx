import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { I18nextProvider, useTranslation } from 'react-i18next';
import { ActivityIndicator, PaperProvider } from 'react-native-paper';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { View } from 'react-native';
import { AuthProvider } from '../lib/auth';
import i18n, { initLanguage } from '../lib/i18n';
import { theme } from '../lib/theme';

const queryClient = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: 1 } } });

function Screens() {
  const { t } = useTranslation();
  return (
    <Stack screenOptions={{ headerStyle: { backgroundColor: theme.colors.primary }, headerTintColor: '#fff' }}>
      <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      <Stack.Screen name="login" options={{ headerShown: false }} />
      <Stack.Screen name="invoice/new" options={{ title: t('invoice.new') }} />
      <Stack.Screen name="invoice/[id]" options={{ title: t('nav.invoices') }} />
      <Stack.Screen name="quotation/new" options={{ title: t('quotation.new') }} />
      <Stack.Screen name="quotation/[id]" options={{ title: t('nav.quotations') }} />
      <Stack.Screen name="credit-note/new" options={{ title: t('creditNote.new') }} />
      <Stack.Screen name="credit-note/[id]" options={{ title: t('nav.creditNotes') }} />
      <Stack.Screen name="credit-notes" options={{ title: t('nav.creditNotes') }} />
      <Stack.Screen name="items" options={{ title: t('nav.items') }} />
      <Stack.Screen name="reports" options={{ title: t('nav.reports') }} />
      <Stack.Screen name="purchases" options={{ title: t('nav.purchases') }} />
      <Stack.Screen name="suppliers" options={{ title: t('nav.suppliers') }} />
    </Stack>
  );
}

export default function RootLayout() {
  const [ready, setReady] = useState(false);
  useEffect(() => {
    initLanguage().finally(() => setReady(true));
  }, []);

  return (
    <SafeAreaProvider>
      <PaperProvider theme={theme}>
        <I18nextProvider i18n={i18n}>
          <QueryClientProvider client={queryClient}>
            <AuthProvider>
              <StatusBar style="light" />
              {ready ? (
                <Screens />
              ) : (
                <View style={{ flex: 1, justifyContent: 'center' }}>
                  <ActivityIndicator />
                </View>
              )}
            </AuthProvider>
          </QueryClientProvider>
        </I18nextProvider>
      </PaperProvider>
    </SafeAreaProvider>
  );
}
