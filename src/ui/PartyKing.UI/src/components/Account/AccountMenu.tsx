import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { MenuButton } from '@/components/common/MenuButton';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { accountApi } from '@/service/endpoints/account/accountApi';
import { useAccount } from '@/service/endpoints/account/useAccount';
import { identityLinks } from '@/service/identityLinks';

const buttonClass = 'site-bar__account-button max-w-[45vw]';

/**
 * The account button of the site bar. The accounts live in the identity service, so a visitor gets "Log in", a link to
 * its page (which sends them back here, and from where they can sign up), and a signed-in account its username with
 * "Account settings" (a link to the identity service) and "Log out". The other sites are in the bar itself.
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
        className={buttonClass}
        entries={[
          { label: t('account.settings'), onClick: () => window.location.assign(identityLinks.settings()) },
          { label: t('account.logOut'), onClick: () => void logOut() },
        ]}
      />
    );
  }

  return (
    <a className={buttonClass} href={identityLinks.login()}>
      {t('account.logIn')}
    </a>
  );
};
