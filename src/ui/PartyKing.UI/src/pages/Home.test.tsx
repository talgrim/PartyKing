import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { Home } from './Home';
import { setLanguage } from '@/i18n';

const mocks = vi.hoisted(() => ({ getSessions: vi.fn(), me: vi.fn(), getDemoConfig: vi.fn(), startDemo: vi.fn(), listMine: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { getSessions: mocks.getSessions } }));
vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/demo/demoApi', () => ({
  demoApi: { getConfig: mocks.getDemoConfig, start: mocks.startDemo, listMine: mocks.listMine },
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
    mocks.listMine.mockReset().mockResolvedValue({ sessions: [], serverTime: '2026-11-13T12:00:00Z' });
    mocks.getDemoConfig.mockReset().mockResolvedValue({ enabled: false, serverTime: '2026-11-13T12:00:00Z' });
    mocks.getSessions.mockReset().mockResolvedValue([
      { id: 'session-1', code: 'ABC123', name: 'Party', hasHost: true },
    ]);
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

    it('lists the visitor\'s own running demo sessions above the start button, so they can return to one', async () => {
      mocks.listMine.mockResolvedValue({
        sessions: [{ sessionId: 's1', name: 'Demo party', expiresAt: new Date(Date.now() + 3600 * 1000).toISOString() }],
        serverTime: new Date().toISOString(),
      });

      renderDemo();

      expect(await screen.findByRole('button', { name: 'Back to the party' })).toBeVisible();
      expect(screen.getByRole('button', { name: 'Start the demo' })).toBeVisible();
    });

    it('shows nothing, not the session list, until it is known whether this is a demo', () => {
      mocks.getDemoConfig.mockReturnValue(new Promise(() => {}));

      renderDemo();

      expect(screen.queryByRole('heading')).not.toBeInTheDocument();
      expect(mocks.getSessions).not.toHaveBeenCalled();
    });

    it('shows the ordinary list when the server answers that it is not a demo', async () => {
      mocks.getDemoConfig.mockRejectedValue({ status: 404, message: 'Not found' });

      renderDemo();

      expect(await screen.findByText('Party')).toBeVisible();
      expect(screen.queryByRole('button', { name: 'Start the demo' })).not.toBeInTheDocument();
    });

    describe('while the server is still starting', () => {
      beforeEach(() => {
        vi.useFakeTimers();
      });

      afterEach(() => {
        vi.useRealTimers();
      });

      it('waits and offers the demo once the API answers, instead of showing the session list', async () => {
        mocks.getDemoConfig
          .mockRejectedValueOnce({ status: 502, message: 'Bad Gateway' })
          .mockRejectedValueOnce({ message: 'Network Error' });

        renderDemo();
        await act(() => vi.advanceTimersByTimeAsync(0));

        expect(screen.getByText('Waking up the server, just a moment…')).toBeVisible();
        expect(screen.queryByText('Could not load the sessions')).not.toBeInTheDocument();
        expect(mocks.getSessions).not.toHaveBeenCalled();

        await act(() => vi.advanceTimersByTimeAsync(5000));

        expect(screen.getByRole('button', { name: 'Start the demo' })).toBeVisible();
        expect(mocks.getSessions).not.toHaveBeenCalled();
        expect(mocks.getDemoConfig).toHaveBeenCalledTimes(3);
      });

      it('gives up after 20 retries and shows the ordinary list', async () => {
        mocks.getDemoConfig.mockRejectedValue({ status: 503, message: 'Service Unavailable' });

        renderDemo();
        await act(() => vi.advanceTimersByTimeAsync(40000));

        expect(screen.getByText('Party')).toBeVisible();
        expect(mocks.getDemoConfig).toHaveBeenCalledTimes(21);
      });
    });
  });

  it('shows the page in Polish, with the host mark and the empty search result interpolated', async () => {
    await setLanguage('pl');

    await renderPage();

    expect(screen.getByRole('heading', { name: 'Dołącz do imprezy' })).toBeInTheDocument();
    expect(screen.getByText('Jest gospodarz')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Chroniona hasłem' })).toBeInTheDocument();

    await userEvent.type(screen.getByRole('textbox', { name: 'Szukaj sesji po kodzie' }), 'zzz');

    expect(screen.getByText('Żadna sesja nie pasuje do „zzz”.')).toBeInTheDocument();
  });
});
