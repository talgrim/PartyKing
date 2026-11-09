import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { Home } from './Home';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';
import { setLanguage } from '@/i18n';

const mocks = vi.hoisted(() => ({ getSessions: vi.fn(), me: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { getSessions: mocks.getSessions } }));
vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));

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
