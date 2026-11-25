import { ReactNode } from 'react';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import { useTranslation } from 'react-i18next';
import { MenuButton } from '@/components/common/MenuButton';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { accountApi } from '@/service/endpoints/account/accountApi';
import { useAccount } from '@/service/endpoints/account/useAccount';
import { identityLinks } from '@/service/identityLinks';
import { CURRENT_SITE, SITE_IDS, siteLinks } from '@/service/siteLinks';

/** Every item of the header has the same height, whatever it holds. */
export const headerItemClass =
  'flex h-7 items-center gap-1.5 px-2 rounded-md font-semibold text-slate-300 hover:bg-slate-700 cursor-pointer';

/** Icons of the header items share one size. */
export const headerIconClass = 'text-base!';

const buttonClass = `${headerItemClass} max-w-[45vw]`;

/**
 * The account part of the header. The accounts live in the identity service, so a visitor gets "Log in" and "Sign up"
 * links to its pages (which send them back here), and a signed-in account its username with "Account settings" (a link
 * to the identity service) and "Log out", under the same list of sites as the menus of the other sites.
 */
export const AccountMenu = (): ReactNode => {
  const { t } = useTranslation();
  const { account, signedOut } = useAccount();

  // A failed request leaves the account shown, so it can be tried again; a lost login is the same as logged out
  const logOut = async () => {
    try {
      await accountApi.logout();
      signedOut();
    } catch (error) {
      if ((error as { status?: number })?.status === HTTP_STATUS_CODE.UNAUTHORIZED) signedOut();
    }
  };

  if (account) {
    return (
      <MenuButton
        label={account.username}
        icon={<AccountCircleIcon fontSize="inherit" className={`${headerIconClass} shrink-0`} />}
        className={buttonClass}
        entries={[
          ...SITE_IDS.map((site, index) => ({
            label: site === CURRENT_SITE ? `${t(`account.sites.${site}`)} · ${t('account.here')}` : t(`account.sites.${site}`),
            href: siteLinks[site],
            current: site === CURRENT_SITE,
            divider: index === SITE_IDS.length - 1,
          })),
          { label: t('account.settings'), onClick: () => window.location.assign(identityLinks.settings()) },
          { label: t('account.logOut'), onClick: () => void logOut() },
        ]}
      />
    );
  }

  return (
    <>
      <a className={buttonClass} href={identityLinks.login()}>
        {t('account.logIn')}
      </a>
      <a className={buttonClass} href={identityLinks.register()}>
        {t('account.signUp')}
      </a>
    </>
  );
};
