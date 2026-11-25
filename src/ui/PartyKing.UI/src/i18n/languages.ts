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

/**
 * The choice is also a cookie for the whole of `.talgrim.dev`, shared with the home page and notes, so it is the same on
 * every site and survives a restart of the browser. It holds nothing but the language and is readable by the script.
 */
export const LANGUAGE_COOKIE = 'talgrim-lang';
const ONE_YEAR_SECONDS = 365 * 24 * 60 * 60;

/** The language of the shared cookie, or null when there is none (or it holds something unknown). */
export const readLanguageCookie = (): Language | null => {
  try {
    const entry = document.cookie.split('; ').find((part) => part.startsWith(`${LANGUAGE_COOKIE}=`));
    const value = entry?.slice(LANGUAGE_COOKIE.length + 1);
    return isLanguage(value) ? value : null;
  } catch {
    return null;
  }
};

const writeLanguageCookie = (language: Language): void => {
  try {
    const attributes = [`${LANGUAGE_COOKIE}=${language}`, 'Path=/', `Max-Age=${ONE_YEAR_SECONDS}`, 'SameSite=Lax'];
    if (/(^|\.)talgrim\.dev$/.test(window.location.hostname)) {
      attributes.push('Domain=.talgrim.dev', 'Secure');
    }
    document.cookie = attributes.join('; ');
  } catch {
    // Cookies are unavailable: the choice then lasts as long as the storage does
  }
};

/** The shared cookie first (a choice made on another site), then localStorage, then the default (English). */
export const readStoredLanguage = (): Language => {
  const fromCookie = readLanguageCookie();
  if (fromCookie) {
    return fromCookie;
  }
  try {
    const stored = localStorage.getItem(LANGUAGE_STORAGE_KEY);
    return isLanguage(stored) ? stored : DEFAULT_LANGUAGE;
  } catch {
    return DEFAULT_LANGUAGE;
  }
};

export const storeLanguage = (language: Language): void => {
  writeLanguageCookie(language);
  try {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
  } catch {
    // Storage is unavailable: the choice then only lasts until the page is closed
  }
};
