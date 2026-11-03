import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { Admin } from './Admin';
import { setLanguage } from '@/i18n';

const mocks = vi.hoisted(() => ({ me: vi.fn(), getSessions: vi.fn() }));

vi.mock('@/service/endpoints/admin/adminApi', () => ({ adminApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { getSessions: mocks.getSessions } }));

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
    mocks.me.mockReset();
    mocks.getSessions.mockResolvedValue([
      { id: 'session-1', code: 'ABC123', name: 'Party', hostName: null, hasHost: false },
      { id: 'session-2', code: 'XYZ789', name: 'Wedding', hostName: 'H', hasHost: true },
    ]);
  });

  it('links each session to its games page', async () => {
    mocks.me.mockResolvedValue(undefined);

    renderPage();

    const links = await screen.findAllByRole('link', { name: 'Games' });
    expect(links.map((link) => link.getAttribute('href'))).toEqual(['/s/session-1/games', '/s/session-2/games']);
  });

  it('shows no games links before the admin has logged in', async () => {
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    renderPage();

    await screen.findByLabelText(/password/i);
    expect(screen.queryByRole('link', { name: 'Games' })).not.toBeInTheDocument();
  });

  it('shows the session list in Polish', async () => {
    await setLanguage('pl');
    mocks.me.mockResolvedValue(undefined);

    renderPage();

    expect(await screen.findAllByRole('link', { name: 'Gry' })).toHaveLength(2);
    expect(screen.getByText('Brak gospodarza')).toBeInTheDocument();
    expect(screen.getByText('Gospodarz: H')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Wyloguj się' })).toBeInTheDocument();
  });

  it('asks for the delete confirmation in the selected language', async () => {
    await setLanguage('pl');
    mocks.me.mockResolvedValue(undefined);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderPage();
    await userEvent.click((await screen.findAllByRole('button', { name: 'Usuń' }))[0]);

    expect(confirm).toHaveBeenCalledWith('Usunąć „Party” (ABC123) razem z kolejką i zdjęciami?');
    confirm.mockRestore();
  });
});
