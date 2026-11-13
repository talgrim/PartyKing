import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { Home } from './Home';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';
import { setLanguage } from '@/i18n';

const mocks = vi.hoisted(() => ({ getSessions: vi.fn(), me: vi.fn(), getDemoConfig: vi.fn(), startDemo: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { getSessions: mocks.getSessions } }));
vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/demo/demoApi', () => ({
  demoApi: { getConfig: mocks.getDemoConfig, start: mocks.startDemo },
}));

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
    mocks.getDemoConfig.mockResolvedValue({ enabled: false, serverTime: '2026-11-13T12:00:00Z' });
    mocks.getSessions.mockReset().mockResolvedValue([
      { id: 'session-1', code: 'ABC123', name: 'Party', hostName: 'H', hasHost: true },
    ]);
  });

  it('shows a visitor without an account no admin link and does not ask who is signed in', async () => {
    await renderPage();

    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
    expect(mocks.me).not.toHaveBeenCalled();
  });

  it('links a signed-in admin to the admin panel', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockResolvedValue({ id: 'a1', username: 'boss', role: 'Admin', createdAt: '2026-11-09T12:00:00Z' });

    await renderPage();

    expect(await screen.findByRole('link', { name: 'Admin panel' })).toHaveAttribute('href', '/admin');
  });

  it('shows a signed-in user no admin link', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockResolvedValue({ id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' });

    await renderPage();

    await waitFor(() => expect(mocks.me).toHaveBeenCalled());
    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
  });

  it('hides the link when the login has expired', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    await renderPage();

    await waitFor(() => expect(mocks.me).toHaveBeenCalled());
    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
  });

  describe('on a demo server', () => {
    beforeEach(() => {
      mocks.getDemoConfig.mockResolvedValue({ enabled: true, serverTime: '2026-11-13T12:00:00Z' });
    });

    const renderDemo = () =>
      render(
        <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
          <MemoryRouter>
            <Home />
          </MemoryRouter>
        </SWRConfig>,
      );

    it('offers to start a demo instead of listing the sessions of everybody else', async () => {
      renderDemo();

      expect(await screen.findByRole('button', { name: 'Start the demo' })).toBeVisible();
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
      expect(mocks.getSessions).not.toHaveBeenCalled();
    });

    it('shows nothing, not the session list, until it is known whether this is a demo', () => {
      mocks.getDemoConfig.mockReturnValue(new Promise(() => {}));

      renderDemo();

      expect(screen.queryByRole('heading')).not.toBeInTheDocument();
      expect(mocks.getSessions).not.toHaveBeenCalled();
    });

    it('shows the ordinary list when the server cannot say', async () => {
      mocks.getDemoConfig.mockRejectedValue({ status: 404, message: 'Not found' });

      renderDemo();

      expect(await screen.findByText('Party')).toBeVisible();
      expect(screen.queryByRole('button', { name: 'Start the demo' })).not.toBeInTheDocument();
    });
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
