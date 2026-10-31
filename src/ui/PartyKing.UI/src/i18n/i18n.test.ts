import { i18n, setLanguage } from '@/i18n';
import { LANGUAGE_STORAGE_KEY, readStoredLanguage } from '@/i18n/languages';
import en from '@/i18n/locales/en.json';
import pl from '@/i18n/locales/pl.json';

const keysOf = (node: object, prefix = ''): string[] =>
  Object.entries(node).flatMap(([key, value]) =>
    typeof value === 'object' && value !== null ? keysOf(value, `${prefix}${key}.`) : [`${prefix}${key}`],
  );

// Polish has more plural forms (_few, _many) than English, so plural variants count as one key
const PLURAL_SUFFIX = /_(zero|one|two|few|many|other)$/;
const baseKeysOf = (node: object): string[] => [...new Set(keysOf(node).map((key) => key.replace(PLURAL_SUFFIX, '')))].sort();

describe('i18n', () => {
  it('has the same keys in English and Polish', () => {
    expect(baseKeysOf(pl)).toEqual(baseKeysOf(en));
  });

  it('has the plural forms of each language', () => {
    const pluralBases = (node: object, forms: string[]) =>
      keysOf(node).filter((key) => key.endsWith('_one')).map((key) => key.replace(/_one$/, '')).flatMap((base) =>
        forms.map((form) => `${base}_${form}`),
      );
    const has = (node: object) => new Set(keysOf(node));
    const missing = (node: object, forms: string[]) => pluralBases(node, forms).filter((key) => !has(node).has(key));

    expect(missing(en, ['one', 'other'])).toEqual([]);
    expect(missing(pl, ['one', 'few', 'many', 'other'])).toEqual([]);
  });

  it('has no empty translations', () => {
    const empty = (node: object): boolean =>
      Object.values(node).some((value) => (typeof value === 'object' && value !== null ? empty(value) : value === ''));
    expect(empty(en)).toBe(false);
    expect(empty(pl)).toBe(false);
  });

  it('starts in English', () => {
    expect(i18n.language).toBe('en');
    expect(document.documentElement.lang).toBe('en');
  });

  it('switches, remembers the language and updates the html lang', async () => {
    await setLanguage('pl');

    expect(i18n.t('language.label')).toBe('Język');
    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('pl');
    expect(document.documentElement.lang).toBe('pl');
  });

  it('follows a change made in another tab', async () => {
    window.dispatchEvent(new StorageEvent('storage', { key: LANGUAGE_STORAGE_KEY, newValue: 'pl' }));

    await vi.waitFor(() => expect(i18n.language).toBe('pl'));
  });

  it('ignores an unknown language in the storage event', async () => {
    window.dispatchEvent(new StorageEvent('storage', { key: LANGUAGE_STORAGE_KEY, newValue: 'de' }));

    expect(i18n.language).toBe('en');
  });

  describe('readStoredLanguage', () => {
    it('falls back to English for a missing or invalid value', () => {
      expect(readStoredLanguage()).toBe('en');
      localStorage.setItem(LANGUAGE_STORAGE_KEY, 'de');
      expect(readStoredLanguage()).toBe('en');
    });

    it('reads a saved language', () => {
      localStorage.setItem(LANGUAGE_STORAGE_KEY, 'pl');
      expect(readStoredLanguage()).toBe('pl');
    });

    it('falls back to English when storage throws', () => {
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new Error('blocked');
      });
      expect(readStoredLanguage()).toBe('en');
      vi.restoreAllMocks();
    });
  });
});
