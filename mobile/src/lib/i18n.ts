import { I18nManager, Platform } from 'react-native';
import * as Localization from 'expo-localization';
import * as Updates from 'expo-updates';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ar, en } from '@fatoura/shared';
import { storage } from './storage';

export type Language = 'en' | 'ar';
const KEY = 'fatoura.language';

void i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, ar: { translation: ar } },
  lng: 'en',
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
});

function applyDirection(lang: Language) {
  if (Platform.OS === 'web') {
    // react-native-web ignores I18nManager; set the document direction ourselves.
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = lang;
  }
}

/** Restores the saved language (or the device language) at start-up. */
export async function initLanguage(): Promise<Language> {
  const saved = (await storage.get(KEY)) as Language | null;
  const device = Localization.getLocales()[0]?.languageCode === 'ar' ? 'ar' : 'en';
  const lang: Language = saved ?? device;
  await i18n.changeLanguage(lang);
  I18nManager.allowRTL(true);
  applyDirection(lang);
  return lang;
}

/**
 * Switches language. Native layout direction only changes after a reload, so the app restarts
 * when the direction flips (I18nManager.forceRTL + Updates.reloadAsync).
 */
export async function setLanguage(lang: Language) {
  await storage.set(KEY, lang);
  await i18n.changeLanguage(lang);
  applyDirection(lang);
  const rtl = lang === 'ar';
  if (Platform.OS !== 'web' && I18nManager.isRTL !== rtl) {
    I18nManager.forceRTL(rtl);
    try {
      await Updates.reloadAsync();
    } catch {
      /* Expo Go / dev client: the direction applies on the next launch */
    }
  }
}

export default i18n;
