import { GlobalOutlined } from '@ant-design/icons';
import { Button } from 'antd';
import { useTranslation } from 'react-i18next';
import { fetchClient } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { applyLanguage } from '../i18n';

/** Toggles Arabic/English (with RTL) and remembers the choice on the user's profile. */
export function LanguageSwitch({ ghost }: { ghost?: boolean }) {
  const { t, i18n } = useTranslation();
  const { user, updateUser } = useAuth();
  const toggle = async () => {
    const next = i18n.language === 'ar' ? 'en' : 'ar';
    applyLanguage(next);
    if (user) {
      const { data } = await fetchClient.PUT('/api/auth/me', { body: { preferredLanguage: next } });
      if (data) updateUser(data);
    }
  };
  return (
    <Button icon={<GlobalOutlined />} onClick={toggle} ghost={ghost} data-testid="language-switch">
      {t('nav.language')}
    </Button>
  );
}
