import { useTranslation } from 'react-i18next';
import { Button } from 'react-native-paper';
import { setLanguage } from '../lib/i18n';

export function LanguageToggle() {
  const { t, i18n } = useTranslation();
  return (
    <Button icon="translate" mode="text" onPress={() => setLanguage(i18n.language === 'ar' ? 'en' : 'ar')} testID="language-toggle">
      {t('nav.language')}
    </Button>
  );
}
