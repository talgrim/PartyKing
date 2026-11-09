import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { Home } from './Home';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';
import { setLanguage } from '@/i18n';

const mocks = vi.hoisted(() => ({ getSessions: vi.fn(), me: vi.fn(), logout: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { getSessions: mocks.getSessions } }));
vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me, logout: mocks.logout } }));

const renderPage = async () => {
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter>
        <Home />
      </MemoryRouter>
    </SWRConfig>,
  );
  await screen.findByText('Party');
};

describe('Home', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.me.mockReset();
    mocks.logout.mockReset();
    mocks.getSessions.mockResolvedValue([
      { id: 'session-1', code: 'ABC123', name: 'Party', hostName: 'H', hasHost: true },
    ]);
  });

  it('shows a visitor without an account no admin link and does not ask who is signed in', async () => {
    await renderPage();

    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();
    expect(mocks.me).not.toHaveBeenCalled();
  });

  it('links a signed-in admin to the admin panel and names the account on the button', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockResolvedValue({ id: 'a1', username: 'boss', role: 'Admin', createdAt: '2026-11-09T12:00:00Z' });

    await renderPage();

    expect(await screen.findByRole('link', { name: 'Admin panel' })).toHaveAttribute('href', '/admin');
    expect(screen.getByRole('button', { name: 'boss' })).toBeInTheDocument();
  });

  it('shows a signed-in user no admin link', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockResolvedValue({ id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' });

    await renderPage();

    expect(await screen.findByRole('button', { name: 'ann' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
  });

  describe('the menu of the username button', () => {
    beforeEach(() => {
      localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
      mocks.me.mockResolvedValue({ id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' });
    });

    it('drops "Account settings" and "Log out" instead of opening the dialog straight away', async () => {
      await renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));

      expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Account settings', 'Log out']);
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('opens the account settings from "Account settings"', async () => {
      await renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));
      await userEvent.click(screen.getByRole('menuitem', { name: 'Account settings' }));

      expect(await screen.findByRole('dialog', { name: 'Account settings' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Change password' })).toBeInTheDocument();
    });

    it('logs out from "Log out" and offers to log in again', async () => {
      mocks.logout.mockResolvedValue(undefined);
      await renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));
      await userEvent.click(screen.getByRole('menuitem', { name: 'Log out' }));

      expect(mocks.logout).toHaveBeenCalledTimes(1);
      expect(await screen.findByRole('button', { name: 'Log in' })).toBeInTheDocument();
    });

    it('stays signed in when logging out failed', async () => {
      mocks.logout.mockRejectedValue({ status: 500, message: 'boom' });
      await renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'ann' }));
      await userEvent.click(screen.getByRole('menuitem', { name: 'Log out' }));

      await waitFor(() => expect(mocks.logout).toHaveBeenCalledTimes(1));
      expect(screen.getByRole('button', { name: 'ann' })).toBeInTheDocument();
    });
  });

  it('opens the login dialog from "Log in" without a menu', async () => {
    await renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Log in' }));

    expect(screen.queryByRole('menuitem')).not.toBeInTheDocument();
    expect(await screen.findByRole('dialog', { name: 'Account' })).toBeInTheDocument();
  });

  it('hides the link when the login has expired', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    await renderPage();

    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Log in' })).toBeInTheDocument();
  });

  it('shows the page in Polish, with the host and the empty search result interpolated', async () => {
    await setLanguage('pl');

    await renderPage();

    expect(screen.getByRole('heading', { name: 'Dołącz do imprezy' })).toBeInTheDocument();
    expect(screen.getByText('Gospodarz: H')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Chroniona hasłem' })).toBeInTheDocument();

    await userEvent.type(screen.getByRole('textbox', { name: 'Szukaj sesji po kodzie' }), 'zzz');

    expect(screen.getByText('Żadna sesja nie pasuje do „zzz”.')).toBeInTheDocument();
  });
});
