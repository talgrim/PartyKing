import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { matchPath, useLocation } from 'react-router-dom';
import { ROUTE_PATHS } from '@/router/constants';
import { currentSiteName } from '@/service/sites';
import { pageTitle } from '@/service/title';

/** The title keys of the pages, the more specific path first. A page that is not here (the landing page) is the site itself. */
const PAGES = [
  { path: ROUTE_PATHS.SESSION_GAMES_RESISTANCE_ICONS, key: 'titles.resistanceIcons' },
  { path: ROUTE_PATHS.SESSION_GAMES_RESISTANCE_JOIN, key: 'resistance.joinTitle' },
  { path: ROUTE_PATHS.SESSION_GAMES_RINGO_JOIN, key: 'ringo.joinTitle' },
  { path: ROUTE_PATHS.SESSION_GAMES_RESISTANCE, key: 'titles.resistance' },
  { path: ROUTE_PATHS.SESSION_GAMES_RINGO, key: 'titles.ringo' },
  { path: ROUTE_PATHS.SESSION_GAMES, key: 'games.title' },
  { path: ROUTE_PATHS.ADMIN, key: 'admin.title' },
  { path: ROUTE_PATHS.SESSION, key: 'titles.session' },
] as const;

/** Keeps the tab title in step with the page and the language. */
export const usePageTitle = (): void => {
  const { t, i18n } = useTranslation();
  const { pathname } = useLocation();
  const language = i18n.resolvedLanguage;
  const page = PAGES.find((candidate) => matchPath({ path: candidate.path, end: true }, pathname));
  const pageName = page ? t(page.key) : null;

  useEffect(() => {
    document.title = pageTitle(pageName, currentSiteName(language));
  }, [pageName, language]);
};
