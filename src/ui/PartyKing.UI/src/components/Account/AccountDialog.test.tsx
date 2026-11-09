import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AccountDialog } from './AccountDialog';
import { AccountDto } from '@/service/endpoints/account/types';
import { setLanguage } from '@/i18n';

const mocks = vi.hoisted(() => ({
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
  logoutEverywhere: vi.fn(),
  changePassword: vi.fn(),
  deleteAccount: vi.fn(),
}));

vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: mocks }));

const user: AccountDto = { id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' };
const admin: AccountDto = { id: 'a1', username: 'boss', role: 'Admin', createdAt: '2026-11-09T12:00:00Z' };

const setup = (account: AccountDto | null) => {
  const props = { onClose: vi.fn(), signedIn: vi.fn(), signedOut: vi.fn() };
  render(<AccountDialog open account={account} {...props} />);

  return props;
};

describe('AccountDialog', () => {
  beforeEach(() => {
    Object.values(mocks).forEach((mock) => mock.mockReset());
  });

  it.each([
    ['a visitor', null],
    ['a signed-in account', user],
  ])('has a close button for %s that closes the dialog without any other effect', async (_, account) => {
    const props = setup(account);

    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    expect(props.onClose).toHaveBeenCalledTimes(1);
    expect(props.signedIn).not.toHaveBeenCalled();
    expect(props.signedOut).not.toHaveBeenCalled();
  });

  describe('for a visitor', () => {
    it('logs in with the typed credentials, then reports the login and closes', async () => {
      mocks.login.mockResolvedValue(undefined);
      const props = setup(null);

      await userEvent.type(screen.getByLabelText('Username'), 'ann');
      await userEvent.type(screen.getByLabelText('Password'), 'secret-password');
      await userEvent.click(screen.getByRole('button', { name: 'Log in' }));

      expect(mocks.login).toHaveBeenCalledWith('ann', 'secret-password');
      expect(props.signedIn).toHaveBeenCalledTimes(1);
      expect(props.onClose).toHaveBeenCalledTimes(1);
    });

    it('shows the message of a failed login and stays open', async () => {
      mocks.login.mockRejectedValue({ status: 401, message: 'Invalid username or password' });
      const props = setup(null);

      await userEvent.type(screen.getByLabelText('Username'), 'ann');
      await userEvent.type(screen.getByLabelText('Password'), 'wrong');
      await userEvent.click(screen.getByRole('button', { name: 'Log in' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Invalid username or password');
      expect(props.signedIn).not.toHaveBeenCalled();
      expect(props.onClose).not.toHaveBeenCalled();
    });

    it('does not offer to submit an empty form', () => {
      setup(null);

      expect(screen.getByRole('button', { name: 'Log in' })).toBeDisabled();
    });

    it('signs up only with a password of at least 8 characters', async () => {
      mocks.register.mockResolvedValue(undefined);
      const props = setup(null);

      await userEvent.click(screen.getByRole('tab', { name: 'Sign up' }));
      await userEvent.type(screen.getByLabelText('Username'), 'ann');
      await userEvent.type(screen.getByLabelText('Password'), 'short');
      expect(screen.getByRole('button', { name: 'Sign up' })).toBeDisabled();

      await userEvent.type(screen.getByLabelText('Password'), '-enough');
      await userEvent.click(screen.getByRole('button', { name: 'Sign up' }));

      expect(mocks.register).toHaveBeenCalledWith('ann', 'short-enough');
      expect(props.signedIn).toHaveBeenCalledTimes(1);
    });

    it('opens on the log-in tab again after it was closed on the sign-up tab', async () => {
      const { rerender } = render(<AccountDialog open account={null} onClose={vi.fn()} signedIn={vi.fn()} signedOut={vi.fn()} />);
      await userEvent.click(screen.getByRole('tab', { name: 'Sign up' }));
      expect(screen.getByRole('tab', { name: 'Sign up' })).toHaveAttribute('aria-selected', 'true');

      rerender(<AccountDialog open={false} account={null} onClose={vi.fn()} signedIn={vi.fn()} signedOut={vi.fn()} />);
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      rerender(<AccountDialog open account={null} onClose={vi.fn()} signedIn={vi.fn()} signedOut={vi.fn()} />);

      expect(screen.getByRole('tab', { name: 'Log in' })).toHaveAttribute('aria-selected', 'true');
    });

    it('shows a taken username as the server reported it', async () => {
      mocks.register.mockRejectedValue({ status: 409, message: 'The username is already taken' });
      setup(null);

      await userEvent.click(screen.getByRole('tab', { name: 'Sign up' }));
      await userEvent.type(screen.getByLabelText('Username'), 'ann');
      await userEvent.type(screen.getByLabelText('Password'), 'secret-password');
      await userEvent.click(screen.getByRole('button', { name: 'Sign up' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('The username is already taken');
    });

    it('is shown in Polish', async () => {
      await setLanguage('pl');
      setup(null);

      expect(screen.getByRole('tab', { name: 'Zarejestruj się' })).toBeInTheDocument();
      expect(screen.getByLabelText('Nazwa użytkownika')).toBeInTheDocument();
    });
  });

  describe('for a signed-in account', () => {
    it('is the account settings and names the account', () => {
      setup(user);

      expect(screen.getByRole('dialog', { name: 'Account settings' })).toBeInTheDocument();
      expect(screen.getByText('Signed in as ann')).toBeInTheDocument();
    });

    it('leaves logging out to the menu of the username button', () => {
      setup(user);

      expect(screen.queryByRole('button', { name: 'Log out' })).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Log out everywhere' })).toBeInTheDocument();
    });

    it('changes the password and clears the fields', async () => {
      mocks.changePassword.mockResolvedValue(undefined);
      setup(user);

      await userEvent.type(screen.getByLabelText('Current password'), 'old-password');
      await userEvent.type(screen.getByLabelText('New password'), 'brand-new-password');
      await userEvent.click(screen.getByRole('button', { name: 'Change password' }));

      expect(mocks.changePassword).toHaveBeenCalledWith('old-password', 'brand-new-password');
      expect(await screen.findByRole('alert')).toHaveTextContent('Password changed');
      expect(screen.getByLabelText('Current password')).toHaveValue('');
    });

    it('shows why the password could not be changed', async () => {
      mocks.changePassword.mockRejectedValue({ status: 400, message: 'The current password is incorrect' });
      setup(user);

      await userEvent.type(screen.getByLabelText('Current password'), 'wrong');
      await userEvent.type(screen.getByLabelText('New password'), 'brand-new-password');
      await userEvent.click(screen.getByRole('button', { name: 'Change password' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('The current password is incorrect');
    });

    it('logs out everywhere only after the confirmation', async () => {
      mocks.logoutEverywhere.mockResolvedValue(undefined);
      const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true);
      const props = setup(user);

      await userEvent.click(screen.getByRole('button', { name: 'Log out everywhere' }));
      expect(mocks.logoutEverywhere).not.toHaveBeenCalled();

      await userEvent.click(screen.getByRole('button', { name: 'Log out everywhere' }));
      expect(mocks.logoutEverywhere).toHaveBeenCalledTimes(1);
      expect(props.signedOut).toHaveBeenCalledTimes(1);
      confirm.mockRestore();
    });

    it('deletes the account after the password is typed again', async () => {
      mocks.deleteAccount.mockResolvedValue(undefined);
      const props = setup(user);

      await userEvent.click(screen.getByRole('button', { name: 'Delete account' }));
      await userEvent.type(screen.getByLabelText('Password to confirm'), 'secret-password');
      await userEvent.click(screen.getByRole('button', { name: 'Delete my account' }));

      expect(mocks.deleteAccount).toHaveBeenCalledWith('secret-password');
      expect(props.signedOut).toHaveBeenCalledTimes(1);
      expect(props.onClose).toHaveBeenCalledTimes(1);
    });

    it('keeps the account when the password to delete it is wrong', async () => {
      mocks.deleteAccount.mockRejectedValue({ status: 400, message: 'The password is incorrect' });
      const props = setup(user);

      await userEvent.click(screen.getByRole('button', { name: 'Delete account' }));
      await userEvent.type(screen.getByLabelText('Password to confirm'), 'wrong');
      await userEvent.click(screen.getByRole('button', { name: 'Delete my account' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('The password is incorrect');
      expect(props.signedOut).not.toHaveBeenCalled();
    });

    it('does not offer an admin to delete the account', () => {
      setup(admin);

      expect(screen.queryByRole('button', { name: 'Delete account' })).not.toBeInTheDocument();
    });

    it('leaves the signed-in view when the login was lost meanwhile', async () => {
      mocks.changePassword.mockRejectedValue({ status: 401, message: 'Unauthorized' });
      const props = setup(user);

      await userEvent.type(screen.getByLabelText('Current password'), 'old-password');
      await userEvent.type(screen.getByLabelText('New password'), 'brand-new-password');
      await userEvent.click(screen.getByRole('button', { name: 'Change password' }));

      expect(props.signedOut).toHaveBeenCalledTimes(1);
    });
  });
});
