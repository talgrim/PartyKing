/** The sites of this family, listed the same way in the account menu of each of them. */
export const SITE_IDS = ['home', 'party', 'notes'] as const;
export type SiteId = (typeof SITE_IDS)[number];

/** The site this app is, which the menu marks as the current one. */
export const CURRENT_SITE: SiteId = 'party';

export const siteLinks: Record<SiteId, string> = {
  home: 'https://talgrim.dev/',
  party: 'https://party.talgrim.dev/',
  notes: 'https://notes.talgrim.dev/',
};
