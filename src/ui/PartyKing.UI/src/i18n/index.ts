import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './locales/en.json';
import pl from './locales/pl.json';
import { DEFAULT_LANGUAGE, isLanguage, Language, LANGUAGE_STORAGE_KEY, readStoredLanguage, storeLanguage, SUPPORTED_LANGUAGES } from './languages';

void i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, pl: { translation: pl } },
  lng: readStoredLanguage(),
  fallbackLng: DEFAULT_LANGUAGE,
  supportedLngs: [...SUPPORTED_LANGUAGES],
  interpolation: { escapeValue: false }, // React escapes already
  initAsync: false, // the resources are bundled, so the first render already has its texts
  react: { useSuspense: false },
});

// The page language drives hyphenation, screen readers and browser translation offers
i18n.on('languageChanged', (language) => {
  document.documentElement.lang = language;
});
document.documentElement.lang = i18n.language;

// Another tab (a game opened from the session page, for instance) changed the language
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === LANGUAGE_STORAGE_KEY && isLanguage(event.newValue) && event.newValue !== i18n.language) {
      void i18n.changeLanguage(event.newValue);
    }
  });
}

/** Switches the whole UI to the language and remembers it for the next visit and for the other tabs. */
export const setLanguage = async (language: Language): Promise<void> => {
  storeLanguage(language);
  await i18n.changeLanguage(language);
};

export { i18n };
