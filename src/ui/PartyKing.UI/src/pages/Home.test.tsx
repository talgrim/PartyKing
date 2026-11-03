import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { Home } from './Home';
import { ADMIN_HINT_KEY } from '@/service/endpoints/admin/useIsAdmin';
import { setLanguage } from '@/i18n';

const mocks = vi.hoisted(() => ({ getSessions: vi.fn(), me: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { getSessions: mocks.getSessions } }));
vi.mock('@/service/endpoints/admin/adminApi', () => ({ adminApi: { me: mocks.me } }));

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
    mocks.getSessions.mockResolvedValue([
      { id: 'session-1', code: 'ABC123', name: 'Party', hostName: 'H', hasHost: true },
    ]);
  });

  it('shows a visitor without an admin login no admin link and does not call the admin API', async () => {
    await renderPage();

    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
    expect(mocks.me).not.toHaveBeenCalled();
  });

  it('links a logged-in admin to the admin panel', async () => {
    localStorage.setItem(ADMIN_HINT_KEY, 'true');
    mocks.me.mockResolvedValue(undefined);

    await renderPage();

    expect(await screen.findByRole('link', { name: 'Admin panel' })).toHaveAttribute('href', '/admin');
  });

  it('hides the link when the admin login has expired', async () => {
    localStorage.setItem(ADMIN_HINT_KEY, 'true');
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    await renderPage();

    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
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
