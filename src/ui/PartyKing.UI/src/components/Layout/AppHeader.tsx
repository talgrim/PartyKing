import { ReactNode } from 'react';
import HomeIcon from '@mui/icons-material/Home';
import { useTranslation } from 'react-i18next';
import { Link, useLocation } from 'react-router-dom';
import { AccountMenu, headerIconClass, headerItemClass } from '@/components/Account/AccountMenu';
import { BacCalculatorButton } from '@/components/Bac/BacCalculatorButton';
import { ROUTE_PATHS } from '@/router/constants';

/**
 * The header in the upper-right corner of every page: a home icon that leads back to all the sessions (not on the
 * landing page, which is that list), the blood alcohol calculator (for everybody) and the account button. Fixed and below the full-screen overlays (z-40, like the language
 * switcher in the opposite corner), so those cover it instead of colliding with their own buttons.
 */
export const AppHeader = (): ReactNode => {
  const { t } = useTranslation();
  const { pathname } = useLocation();

  return (
    <header className="fixed top-1 right-1 z-40 flex items-center gap-1 p-0.5 rounded-md bg-slate-900/80 text-xs">
      {pathname !== ROUTE_PATHS.HOME && (
        <Link
          to={ROUTE_PATHS.HOME}
          aria-label={t('admin.allSessions')}
          title={t('admin.allSessions')}
          className={headerItemClass}
        >
          <HomeIcon fontSize="inherit" className={headerIconClass} />
        </Link>
      )}
      <BacCalculatorButton />
      <AccountMenu />
    </header>
  );
};
