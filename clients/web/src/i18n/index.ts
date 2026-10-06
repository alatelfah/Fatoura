import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import dayjs from 'dayjs';
import updateLocale from 'dayjs/plugin/updateLocale';
import 'dayjs/locale/ar';
import { ar, en } from '@fatoura/shared';

export type Language = 'en' | 'ar';
const STORAGE_KEY = 'fatoura.language';

function storedLanguage(): Language {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'ar' ? 'ar' : 'en';
  } catch {
    return 'en';
  }
}

// UAE documents use Latin digits; stop dayjs's Arabic locale from converting them.
dayjs.extend(updateLocale);
dayjs.updateLocale('ar', { postformat: (s: string) => s, preparse: (s: string) => s });

void i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, ar: { translation: ar } },
  lng: storedLanguage(),
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
});

/** Keeps <html lang/dir>, dayjs and storage in sync with i18next. */
export function applyLanguage(lang: Language) {
  document.documentElement.lang = lang;
  document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
  dayjs.locale(lang);
  try {
    localStorage.setItem(STORAGE_KEY, lang);
  } catch {
    /* storage unavailable: keep the in-memory choice */
  }
  if (i18n.language !== lang) void i18n.changeLanguage(lang);
}

applyLanguage(storedLanguage());

export default i18n;
