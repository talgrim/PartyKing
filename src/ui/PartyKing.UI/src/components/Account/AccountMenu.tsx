import { ReactNode, useState } from 'react';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import { useTranslation } from 'react-i18next';
import { MenuButton } from '@/components/common/MenuButton';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { accountApi } from '@/service/endpoints/account/accountApi';
import { useAccount } from '@/service/endpoints/account/useAccount';
import { AccountDialog } from './AccountDialog';

/** Every item of the header has the same height, whatever it holds. */
export const headerItemClass =
  'flex h-7 items-center gap-1.5 px-2 rounded-md font-semibold text-slate-300 hover:bg-slate-700 cursor-pointer';

/** Icons of the header items share one size. */
export const headerIconClass = 'text-base!';

const buttonClass = `${headerItemClass} max-w-[45vw]`;

/**
 * The account button of the header: "Log in" for a visitor (it opens the dialog), the username for a signed-in
 * account (it drops "Account settings" and "Log out").
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
    <>
      {account ? (
        <MenuButton
          label={account.username}
          icon={<AccountCircleIcon fontSize="inherit" className={`${headerIconClass} shrink-0`} />}
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
    </>
  );
};
