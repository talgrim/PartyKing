export const SUPPORTED_LANGUAGES = ['en', 'pl'] as const;

export type Language = (typeof SUPPORTED_LANGUAGES)[number];

export const DEFAULT_LANGUAGE: Language = 'en';

export const LANGUAGE_STORAGE_KEY = 'partyking.language';

/** Each language is named in itself, so it stays recognisable whatever language is selected. */
export const LANGUAGE_NAMES: Record<Language, string> = {
  en: 'English',
  pl: 'Polski',
};

export const isLanguage = (value: unknown): value is Language => SUPPORTED_LANGUAGES.some((language) => language === value);

/** The language saved in localStorage, or the default (English) when nothing valid is saved or storage fails. */
export const readStoredLanguage = (): Language => {
  try {
    const stored = localStorage.getItem(LANGUAGE_STORAGE_KEY);
    return isLanguage(stored) ? stored : DEFAULT_LANGUAGE;
  } catch {
    return DEFAULT_LANGUAGE;
  }
};

export const storeLanguage = (language: Language): void => {
  try {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
  } catch {
    // Storage is unavailable: the choice then only lasts until the page is closed
  }
};
