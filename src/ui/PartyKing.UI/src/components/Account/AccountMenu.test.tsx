import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { AccountMenu } from './AccountMenu';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';

const mocks = vi.hoisted(() => ({ me: vi.fn(), logout: vi.fn() }));

vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me, logout: mocks.logout } }));

const ann = { id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' };

const renderMenu = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <AccountMenu />
    </SWRConfig>,
  );

describe('AccountMenu', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.me.mockReset();
    mocks.logout.mockReset();
  });

  describe('for a visitor', () => {
    it('offers to log in without asking who is signed in', () => {
      renderMenu();

      expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();
      expect(mocks.me).not.toHaveBeenCalled();
    });

    it('opens the login dialog from "Log in", with no menu', async () => {
      renderMenu();

      await userEvent.click(screen.getByRole('button', { name: 'Log in' }));

      expect(screen.queryByRole('menuitem')).not.toBeInTheDocument();
      expect(await screen.findByRole('dialog', { name: 'Account' })).toBeInTheDocument();
    });
  });

  describe('for a signed-in account', () => {
    beforeEach(() => {
      localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
      mocks.me.mockResolvedValue(ann);
    });

    it('names the account on the button and drops "Account settings" and "Log out"', async () => {
      renderMenu();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));

      expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Account settings', 'Log out']);
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('shows a person icon to the right of the username', async () => {
      renderMenu();

      const button = await screen.findByRole('button', { name: 'ann' });

      expect(button.lastElementChild).toBe(screen.getByTestId('AccountCircleIcon'));
    });

    it('opens the account settings from "Account settings"', async () => {
      renderMenu();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));
      await userEvent.click(screen.getByRole('menuitem', { name: 'Account settings' }));

      expect(await screen.findByRole('dialog', { name: 'Account settings' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Change password' })).toBeInTheDocument();
    });

    it('logs out from "Log out" and offers to log in again', async () => {
      mocks.logout.mockResolvedValue(undefined);
      renderMenu();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));
      await userEvent.click(screen.getByRole('menuitem', { name: 'Log out' }));

      expect(mocks.logout).toHaveBeenCalledTimes(1);
      expect(await screen.findByRole('button', { name: 'Log in' })).toBeInTheDocument();
    });

    it('stays signed in when logging out failed', async () => {
      mocks.logout.mockRejectedValue({ status: 500, message: 'boom' });
      renderMenu();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));
      await userEvent.click(screen.getByRole('menuitem', { name: 'Log out' }));

      await waitFor(() => expect(mocks.logout).toHaveBeenCalledTimes(1));
      expect(screen.getByRole('button', { name: 'ann' })).toBeInTheDocument();
    });

    it('offers to log in once the login has expired', async () => {
      mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });
      renderMenu();

      expect(await screen.findByRole('button', { name: 'Log in' })).toBeInTheDocument();
    });
  });

  it('shows a login made elsewhere on the page, such as the admin form, without a reload', async () => {
    mocks.me.mockResolvedValue(ann);
    const { useAccount } = await import('@/service/endpoints/account/useAccount');
    const Elsewhere = () => <button onClick={useAccount().signedIn}>login elsewhere</button>;
    render(
      <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
        <AccountMenu />
        <Elsewhere />
      </SWRConfig>,
    );
    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'login elsewhere' }));

    expect(await screen.findByRole('button', { name: 'ann' })).toBeInTheDocument();
  });
});
