import kit from '@/kit/sites.json';
import { DEFAULT_LANGUAGE, isLanguage, Language } from '@/i18n/languages';

// Synced from talgrim/site-kit (see site-kit.json), so the site list is edited there and not here
interface Site {
  id: string;
  name: Record<Language, string>;
  url: string;
  urls?: Partial<Record<Language, string>>;
  switcher: boolean;
}

const sites: readonly Site[] = kit.sites;

export const BRAND: string = kit.brand;

/** The site this app is, which the switcher marks as the current one. */
export const CURRENT_SITE_ID = 'party';

const toLanguage = (code: string | undefined): Language => (code && isLanguage(code) ? code : DEFAULT_LANGUAGE);

const siteUrl = (site: Site, language: Language): string => site.urls?.[language] ?? site.url;

const find = (id: string): Site => {
  const site = sites.find((candidate) => candidate.id === id);
  if (!site) throw new Error(`Unknown site: ${id}`);

  return site;
};

export const currentSiteName = (code: string | undefined): string => find(CURRENT_SITE_ID).name[toLanguage(code)];

export const homeUrl = (code: string | undefined): string => siteUrl(find('home'), toLanguage(code));

/** The sites listed in the switcher, in the order of the kit's list. */
export const switcherSites = (
  code: string | undefined,
): { id: string; name: string; url: string; current: boolean }[] => {
  const language = toLanguage(code);

  return sites
    .filter((site) => site.switcher)
    .map((site) => ({
      id: site.id,
      name: site.name[language],
      url: siteUrl(site, language),
      current: site.id === CURRENT_SITE_ID,
    }));
};
