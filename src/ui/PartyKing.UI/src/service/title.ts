import { BRAND } from '@/service/sites';

/**
 * The tab title: `<Page> · <Site> · talgrim.dev`. A site's main page has no page part (`PartyKing · talgrim.dev`).
 * The format is the same on every talgrim.dev site (see the site-kit docs).
 */
export const pageTitle = (page: string | null, siteName: string): string =>
  [page, siteName, BRAND].filter(Boolean).join(' · ');
