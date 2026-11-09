import { ReactNode, useState } from 'react';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import { useTranslation } from 'react-i18next';
import { MenuButton } from '@/components/common/MenuButton';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { accountApi } from '@/service/endpoints/account/accountApi';
import { useAccount } from '@/service/endpoints/account/useAccount';
import { AccountDialog } from './AccountDialog';

const buttonClass = 'flex max-w-[45vw] items-center gap-1 px-2 py-1 font-semibold text-slate-300 hover:bg-slate-700 cursor-pointer';

/**
 * The account button in the upper-right corner of every page: "Log in" for a visitor (it opens the dialog), the
 * username for a signed-in account (it drops "Account settings" and "Log out"). Fixed and below the full-screen
 * overlays (z-40, like the language switcher in the opposite corner), so those cover it instead of colliding with
 * their own buttons.
 */
export const AccountMenu = (): ReactNode => {
  const { t } = useTranslation();
  const { account, signedIn, signedOut } = useAccount();
  const [dialogOpen, setDialogOpen] = useState(false);

  // A failed request leaves the account shown, so it can be tried again; a lost login is the same as logged out
  const logOut = async () => {
    try {
      await accountApi.logout();
      signedOut();
    } catch (error) {
      if ((error as { status?: number })?.status === HTTP_STATUS_CODE.UNAUTHORIZED) signedOut();
    }
  };

  return (
    <div className="fixed top-1 right-1 z-40 overflow-hidden rounded-md bg-slate-900/80 text-xs">
      {account ? (
        <MenuButton
          label={account.username}
          icon={<AccountCircleIcon fontSize="inherit" className="shrink-0" />}
          className={buttonClass}
          entries={[
            { label: t('account.settings'), onClick: () => setDialogOpen(true) },
            { label: t('account.logOut'), onClick: () => void logOut() },
          ]}
        />
      ) : (
        <button type="button" className={buttonClass} onClick={() => setDialogOpen(true)}>
          {t('account.logIn')}
        </button>
      )}
      <AccountDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        account={account}
        signedIn={signedIn}
        signedOut={signedOut}
      />
    </div>
  );
};
