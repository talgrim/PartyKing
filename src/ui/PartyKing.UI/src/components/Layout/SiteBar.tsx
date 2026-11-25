import { ReactNode, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation } from 'react-router-dom';
import { AccountMenu } from '@/components/Account/AccountMenu';
import { BacCalculatorButton } from '@/components/Bac/BacCalculatorButton';
import { useSiteBarHeight } from '@/components/Layout/useSiteBarHeight';
import { LanguageSwitcher } from '@/components/common/LanguageSwitcher';
import { ROUTE_PATHS } from '@/router/constants';
import { useAccount } from '@/service/endpoints/account/useAccount';
import { useIsDemo } from '@/service/endpoints/demo/useDemoConfig';
import { BRAND, currentSiteName, homeUrl, switcherSites } from '@/service/sites';

/**
 * The bar at the top of every page, the same on every talgrim.dev site (its markup is described in the site-kit docs):
 * PartyKing · talgrim.dev, the other sites for a signed-in visitor, the links of this site (back to all the sessions,
 * not on the landing page which is that list; the blood alcohol calculator; the admin panel for an admin), then the
 * language and the account. It is in the page flow, so the pages take its height off the screen with `--site-bar-h`.
 * A demo has neither the list, the other sites nor accounts, so it keeps the calculator and the language.
 */
export const SiteBar = (): ReactNode => {
  const { t, i18n } = useTranslation();
  const { pathname } = useLocation();
  const isDemo = useIsDemo();
  const { account } = useAccount();
  const bar = useRef<HTMLElement>(null);
  const language = i18n.resolvedLanguage;
  useSiteBarHeight(bar);

  return (
    <header ref={bar} className="site-bar">
      <div className="site-bar__title">
        <Link className="site-bar__site" to={ROUTE_PATHS.HOME}>
          {currentSiteName(language)}
        </Link>
        <a className="site-bar__brand" href={homeUrl(language)}>
          {BRAND}
        </a>
      </div>
      {!isDemo && account && (
        <nav className="site-bar__sites" aria-label={t('account.sitesLabel')}>
          {switcherSites(language).map((site) => (
            <a key={site.id} href={site.url} aria-current={site.current ? 'page' : undefined}>
              {site.name}
            </a>
          ))}
        </nav>
      )}
      <nav className="site-bar__nav" aria-label={currentSiteName(language)}>
        {!isDemo && pathname !== ROUTE_PATHS.HOME && <Link to={ROUTE_PATHS.HOME}>{t('admin.allSessions')}</Link>}
        <BacCalculatorButton />
        {!isDemo && account?.role === 'Admin' && <Link to={ROUTE_PATHS.ADMIN}>{t('home.adminPanel')}</Link>}
      </nav>
      <div className="site-bar__end">
        <LanguageSwitcher />
        {!isDemo && <AccountMenu />}
      </div>
    </header>
  );
};
