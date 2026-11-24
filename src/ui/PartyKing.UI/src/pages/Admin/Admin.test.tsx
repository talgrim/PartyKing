import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { Admin } from './Admin';
import envVariables from '@/envVariables';
import { setLanguage } from '@/i18n';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';

const mocks = vi.hoisted(() => ({ me: vi.fn(), listSessions: vi.fn(), cancelSessionDeletion: vi.fn() }));

vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/admin/adminApi', () => ({
  adminApi: { listSessions: mocks.listSessions, cancelSessionDeletion: mocks.cancelSessionDeletion },
}));

const admin = { id: 'a1', username: 'boss', role: 'Admin', createdAt: '2026-11-09T12:00:00Z' };
const user = { id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' };

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter>
        <Admin />
      </MemoryRouter>
    </SWRConfig>,
  );

describe('Admin', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockReset();
    mocks.listSessions.mockResolvedValue([
      { id: 'session-1', code: 'ABC123', name: 'Party', hostName: null, hasHost: false },
      { id: 'session-2', code: 'XYZ789', name: 'Wedding', hostName: 'H', hasHost: true },
    ]);
  });

  it('links each session to its games page', async () => {
    mocks.me.mockResolvedValue(admin);

    renderPage();

    const links = await screen.findAllByRole('link', { name: 'Games' });
    expect(links.map((link) => link.getAttribute('href'))).toEqual(['/s/session-1/games', '/s/session-2/games']);
  });

  it('links the registered users page of the identity service for an admin', async () => {
    mocks.me.mockResolvedValue(admin);

    renderPage();

    expect(await screen.findByRole('link', { name: 'Registered users' })).toHaveAttribute(
      'href',
      `${envVariables.identityUrl}/admin/users`,
    );
  });

  it('turns a signed-in user away from the admin panel and offers to log out', async () => {
    mocks.me.mockResolvedValue(user);

    renderPage();

    expect(await screen.findByText(/not an admin account/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Games' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Registered users' })).not.toBeInTheDocument();
  });

  it('links a visitor nobody is signed in as to the login of the identity service', async () => {
    localStorage.clear();
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    renderPage();

    const login = await screen.findByRole('link', { name: 'Log in' });
    expect(login).toHaveAttribute('href', expect.stringContaining(`${envVariables.identityUrl}/login?next=`));
    expect(screen.queryByLabelText('Password')).not.toBeInTheDocument();
  });

  it('shows the panel to an admin whose browser lost the account hint, and remembers them again', async () => {
    localStorage.clear();
    mocks.me.mockResolvedValue(admin);

    renderPage();

    expect(await screen.findAllByRole('link', { name: 'Games' })).toHaveLength(2);
    expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('true');
  });

  it('shows no games links before the admin has logged in', async () => {
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    renderPage();

    await screen.findByRole('link', { name: 'Log in' });
    expect(screen.queryByRole('link', { name: 'Games' })).not.toBeInTheDocument();
  });

  it('shows the session list in Polish', async () => {
    await setLanguage('pl');
    mocks.me.mockResolvedValue(admin);

    renderPage();

    expect(await screen.findAllByRole('link', { name: 'Gry' })).toHaveLength(2);
    expect(screen.getByText('Brak gospodarza')).toBeInTheDocument();
    expect(screen.getByText('Gospodarz: H')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Wyloguj się' })).toBeInTheDocument();
  });

  it('asks for the delete confirmation in the selected language', async () => {
    await setLanguage('pl');
    mocks.me.mockResolvedValue(admin);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderPage();
    await userEvent.click((await screen.findAllByRole('button', { name: 'Usuń' }))[0]);

    expect(confirm).toHaveBeenCalledWith('Usunąć „Party” (ABC123) razem z kolejką i zdjęciami?');
    confirm.mockRestore();
  });

  describe('a deletion that a guest asked for', () => {
    const scheduled = { id: 'session-2', code: 'XYZ789', name: 'Wedding', hostName: 'H', hasHost: true, deletionScheduledAt: '2026-11-28T14:05:00Z' };

    beforeEach(() => {
      mocks.cancelSessionDeletion.mockReset().mockResolvedValue(undefined);
      mocks.listSessions.mockResolvedValue([
        { id: 'session-1', code: 'ABC123', name: 'Party', hostName: null, hasHost: false },
        scheduled,
      ]);
    });

    it('shows the date next to the session that has one, and offers to cancel only there', async () => {
      mocks.me.mockResolvedValue(admin);

      renderPage();

      expect(await screen.findByText(/Deletion requested by a guest, scheduled for .*November.*2026/)).toBeVisible();
      expect(screen.getAllByRole('button', { name: 'Cancel deletion' })).toHaveLength(1);
    });

    it('calls the deletion off once confirmed', async () => {
      mocks.me.mockResolvedValue(admin);
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);

      renderPage();
      await userEvent.click(await screen.findByRole('button', { name: 'Cancel deletion' }));

      expect(confirm).toHaveBeenCalledWith('Cancel the deletion of “Wedding”? The session stays.');
      expect(mocks.cancelSessionDeletion).toHaveBeenCalledWith('session-2');
      expect(await screen.findByText('Deletion cancelled')).toBeVisible();
      confirm.mockRestore();
    });

    it('leaves the deletion alone when the confirmation is refused', async () => {
      mocks.me.mockResolvedValue(admin);
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

      renderPage();
      await userEvent.click(await screen.findByRole('button', { name: 'Cancel deletion' }));

      expect(mocks.cancelSessionDeletion).not.toHaveBeenCalled();
      confirm.mockRestore();
    });
  });
});
