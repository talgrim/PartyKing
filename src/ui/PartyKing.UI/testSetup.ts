import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { i18n } from './src/i18n';
import { DEFAULT_LANGUAGE, LANGUAGE_STORAGE_KEY } from './src/i18n/languages';

// Tests are written against the default language; one that switches language must not leak it into the next
afterEach(async () => {
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  await i18n.changeLanguage(DEFAULT_LANGUAGE);
});
