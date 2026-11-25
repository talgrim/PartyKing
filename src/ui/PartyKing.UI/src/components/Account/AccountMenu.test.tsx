import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { AccountMenu } from './AccountMenu';
import { ACCOUNT_HINT_KEY, IDENTITY_HINT_COOKIE } from '@/service/endpoints/account/useAccount';
import envVariables from '@/envVariables';

const mocks = vi.hoisted(() => ({ me: vi.fn(), logout: vi.fn() }));

vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me, logout: mocks.logout } }));

const IDENTITY = envVariables.identityUrl;

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
    document.cookie = `${IDENTITY_HINT_COOKIE}=; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
    mocks.me.mockReset();
    mocks.logout.mockReset();
  });

  describe('for a visitor', () => {
    it('offers to log in and to sign up without asking who is signed in', () => {
      renderMenu();

      expect(screen.getByRole('link', { name: 'Log in' })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Sign up' })).toBeInTheDocument();
      expect(mocks.me).not.toHaveBeenCalled();
    });

    it('links to the identity service, which brings the visitor back to this page', () => {
      renderMenu();

      const next = encodeURIComponent(window.location.href);
      expect(screen.getByRole('link', { name: 'Log in' })).toHaveAttribute('href', `${IDENTITY}/login?next=${next}`);
      expect(screen.getByRole('link', { name: 'Sign up' })).toHaveAttribute('href', `${IDENTITY}/register?next=${next}`);
    });

    it('has no menu and no dialog', () => {
      renderMenu();

      expect(screen.queryByRole('menuitem')).not.toBeInTheDocument();
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('notices a login made on the identity service, whose cookie says to ask', async () => {
      document.cookie = `${IDENTITY_HINT_COOKIE}=1`;
      mocks.me.mockResolvedValue(ann);

      renderMenu();

      expect(await screen.findByRole('button', { name: 'ann' })).toBeInTheDocument();
      expect(mocks.me).toHaveBeenCalledTimes(1);
    });
  });

  describe('for a signed-in account', () => {
    beforeEach(() => {
      localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
      mocks.me.mockResolvedValue(ann);
    });

    it('names the account on the button and drops the sites, "Account settings" and "Log out"', async () => {
      renderMenu();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));

      expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
        'Home',
        'PartyKing · you are here',
        'Notes',
        'Account settings',
        'Log out',
      ]);
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('links the three sites of the family and marks this one as the current page', async () => {
      renderMenu();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));

      expect(screen.getByRole('menuitem', { name: 'Home' })).toHaveAttribute('href', 'https://talgrim.dev/');
      expect(screen.getByRole('menuitem', { name: 'Notes' })).toHaveAttribute('href', 'https://notes.talgrim.dev/');
      const here = screen.getByRole('menuitem', { name: /PartyKing/ });
      expect(here).toHaveAttribute('href', 'https://party.talgrim.dev/');
      expect(here).toHaveAttribute('aria-current', 'page');
      expect(screen.getByRole('menuitem', { name: 'Home' })).not.toHaveAttribute('aria-current');
    });

    it('shows a person icon to the right of the username', async () => {
      renderMenu();

      const button = await screen.findByRole('button', { name: 'ann' });

      expect(button.lastElementChild).toBe(screen.getByTestId('AccountCircleIcon'));
    });

    it('opens the account settings of the identity service from "Account settings"', async () => {
      const assign = vi.fn();
      vi.spyOn(window, 'location', 'get').mockReturnValue({ ...window.location, assign } as Location);
      renderMenu();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));
      await userEvent.click(screen.getByRole('menuitem', { name: 'Account settings' }));

      expect(assign).toHaveBeenCalledWith(`${IDENTITY}/account`);
      vi.restoreAllMocks();
    });

    it('logs out from "Log out" and offers to log in again', async () => {
      mocks.logout.mockResolvedValue(undefined);
      renderMenu();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));
      await userEvent.click(screen.getByRole('menuitem', { name: 'Log out' }));

      expect(mocks.logout).toHaveBeenCalledTimes(1);
      expect(await screen.findByRole('link', { name: 'Log in' })).toBeInTheDocument();
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

      expect(await screen.findByRole('link', { name: 'Log in' })).toBeInTheDocument();
    });
  });
});
