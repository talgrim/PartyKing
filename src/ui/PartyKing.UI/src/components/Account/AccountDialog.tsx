import { FormEvent, ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Dialog, DialogContent, DialogTitle, Tab, Tabs } from '@mui/material';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { accountApi } from '@/service/endpoints/account/accountApi';
import { AccountDto } from '@/service/endpoints/account/types';
import {
  MAX_ACCOUNT_PASSWORD_LENGTH,
  MIN_ACCOUNT_PASSWORD_LENGTH,
  errorMessage,
  inputClass,
  primaryButton,
  secondaryButton,
} from './accountForm';
import { CredentialsForm } from './CredentialsForm';

type Props = {
  open: boolean;
  onClose: () => void;
  /** Who is signed in, or null. */
  account: AccountDto | null;
  signedIn: () => void;
  signedOut: () => void;
};

/** Log in or sign up for a visitor; change password, log out (everywhere) and delete the account for a signed-in one. */
export const AccountDialog = ({ open, onClose, account, signedIn, signedOut }: Props): ReactNode => {
  const { t } = useTranslation();

  const done = () => {
    signedIn();
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} aria-labelledby="account-title" fullWidth maxWidth="xs">
      <DialogTitle id="account-title">{account ? t('account.signedInAs', { name: account.username }) : t('account.title')}</DialogTitle>
      <DialogContent>
        {account ? <SignedIn account={account} signedOut={signedOut} onClose={onClose} /> : <LogInOrSignUp onDone={done} />}
      </DialogContent>
    </Dialog>
  );
};

/** Its tab is state of its own, so the dialog opens on "Log in" every time (MUI unmounts the content when it closes). */
const LogInOrSignUp = ({ onDone }: { onDone: () => void }): ReactNode => {
  const { t } = useTranslation();
  const [tab, setTab] = useState<'login' | 'register'>('login');

  return (
    <div className="space-y-3 pt-1">
      <Tabs value={tab} onChange={(_, value) => setTab(value)} variant="fullWidth">
        <Tab value="login" label={t('account.logIn')} />
        <Tab value="register" label={t('account.signUp')} />
      </Tabs>
      <CredentialsForm key={tab} mode={tab} onDone={onDone} />
    </div>
  );
};

const SignedIn = ({
  account,
  signedOut,
  onClose,
}: {
  account: AccountDto;
  signedOut: () => void;
  onClose: () => void;
}): ReactNode => {
  const { t } = useTranslation();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [deletePassword, setDeletePassword] = useState('');
  const [deleting, setDeleting] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  // A lost login (the cookie expired or was ended elsewhere) leaves the signed-in view
  const run = async (action: () => Promise<unknown>, onSuccess: () => void) => {
    setBusy(true);
    setMessage(null);
    try {
      await action();
      onSuccess();
    } catch (error) {
      if ((error as { status?: number })?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        signedOut();
        return;
      }
      setMessage(errorMessage(error, t('common.somethingWentWrong')));
    } finally {
      setBusy(false);
    }
  };

  const changePassword = (e: FormEvent) => {
    e.preventDefault();
    void run(
      () => accountApi.changePassword(currentPassword, newPassword),
      () => {
        setCurrentPassword('');
        setNewPassword('');
        setMessage(t('account.passwordChanged'));
      },
    );
  };

  const logOut = () =>
    void run(accountApi.logout, () => {
      signedOut();
      onClose();
    });

  const logOutEverywhere = () => {
    if (window.confirm(t('account.confirmLogOutEverywhere'))) {
      void run(accountApi.logoutEverywhere, () => {
        signedOut();
        onClose();
      });
    }
  };

  const deleteAccount = (e: FormEvent) => {
    e.preventDefault();
    void run(
      () => accountApi.deleteAccount(deletePassword),
      () => {
        signedOut();
        onClose();
      },
    );
  };

  return (
    <div className="space-y-4 pt-1">
      <form className="space-y-2" onSubmit={changePassword}>
        <h3 className="text-sm font-semibold uppercase">{t('account.changePassword')}</h3>
        <input
          className={inputClass}
          type="password"
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
          placeholder={t('account.currentPassword')}
          aria-label={t('account.currentPassword')}
          autoComplete="current-password"
          maxLength={MAX_ACCOUNT_PASSWORD_LENGTH}
        />
        <input
          className={inputClass}
          type="password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          placeholder={t('account.newPassword')}
          aria-label={t('account.newPassword')}
          autoComplete="new-password"
          maxLength={MAX_ACCOUNT_PASSWORD_LENGTH}
        />
        <p className="text-sm text-slate-500">{t('account.passwordHint')}</p>
        <button
          type="submit"
          className={primaryButton}
          disabled={busy || !currentPassword || newPassword.length < MIN_ACCOUNT_PASSWORD_LENGTH}
        >
          {t('account.changePassword')}
        </button>
      </form>

      {message && (
        <p role="alert" className="text-sm text-amber-500">
          {message}
        </p>
      )}

      <div className="flex flex-wrap gap-2">
        <button type="button" className={secondaryButton} disabled={busy} onClick={logOut}>
          {t('account.logOut')}
        </button>
        <button type="button" className={secondaryButton} disabled={busy} onClick={logOutEverywhere}>
          {t('account.logOutEverywhere')}
        </button>
        {account.role === 'User' && !deleting && (
          <button type="button" className={`${secondaryButton} text-red-400`} disabled={busy} onClick={() => setDeleting(true)}>
            {t('account.deleteAccount')}
          </button>
        )}
      </div>

      {deleting && (
        <form className="space-y-2" onSubmit={deleteAccount}>
          <p className="text-sm text-red-400">{t('account.deleteWarning')}</p>
          <input
            className={inputClass}
            type="password"
            value={deletePassword}
            onChange={(e) => setDeletePassword(e.target.value)}
            placeholder={t('account.password')}
            aria-label={t('account.passwordToDelete')}
            autoComplete="current-password"
            maxLength={MAX_ACCOUNT_PASSWORD_LENGTH}
          />
          <button type="submit" className={`${secondaryButton} text-red-400`} disabled={busy || !deletePassword}>
            {t('account.confirmDelete')}
          </button>
        </form>
      )}
    </div>
  );
};
