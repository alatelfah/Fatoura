import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App as AntApp, ConfigProvider } from 'antd';
import arEG from 'antd/locale/ar_EG';
import enUS from 'antd/locale/en_US';
import { useTranslation } from 'react-i18next';
import { RouterProvider } from 'react-router';
import { AuthProvider } from './auth/AuthContext';
import { router } from './routes';

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false } },
});

function Themed() {
  const { i18n } = useTranslation();
  const rtl = i18n.language === 'ar';
  return (
    <ConfigProvider
      direction={rtl ? 'rtl' : 'ltr'}
      locale={rtl ? arEG : enUS}
      theme={{
        token: {
          colorPrimary: '#1f3a5f',
          borderRadius: 6,
          fontFamily: rtl
            ? "'Noto Sans Arabic', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif"
            : "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
        },
      }}
    >
      <AntApp>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </AntApp>
    </ConfigProvider>
  );
}

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Themed />
    </QueryClientProvider>
  );
}
