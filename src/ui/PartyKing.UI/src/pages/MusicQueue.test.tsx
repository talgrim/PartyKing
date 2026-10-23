import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { MusicQueue } from './MusicQueue';
import { SongSource } from '@/service/endpoints/queue/types';

const mocks = vi.hoisted(() => ({
  getSession: vi.fn(),
  getViewerRole: vi.fn(),
  getNowPlaying: vi.fn(),
  join: vi.fn(),
  joinWithToken: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: {
    getSession: mocks.getSession,
    getViewerRole: mocks.getViewerRole,
    join: mocks.join,
    joinWithToken: mocks.joinWithToken,
    createJoinToken: vi.fn(),
  },
}));
vi.mock('@/service/endpoints/player/playerApi', () => ({ playerApi: { getNowPlaying: mocks.getNowPlaying } }));
vi.mock('@/components/Music/JoinQrCode', () => ({ JoinQrCode: () => <button>Show QR code</button> }));
vi.mock('@/components/Music/Queue', () => ({ Queue: () => <div>queue</div> }));
vi.mock('@/components/Music/QueueList', () => ({ QueueList: () => <div>queue list</div> }));
vi.mock('@/pages/Slideshow/SlideshowView', () => ({ SlideshowView: () => <div>slideshow</div> }));
vi.mock('@/components/Music/HostPlayer', () => ({ HostPlayer: () => <div>host player</div> }));
vi.mock('@/components/Music/AdminPlayerControls', () => ({ AdminPlayerControls: () => <div>admin controls</div> }));

const renderPage = async () => {
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1']}>
        <Routes>
          <Route path="/s/:sessionId" element={<MusicQueue />} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );
  await screen.findByText('Party');
  await screen.findByText('Track name');
};

describe('MusicQueue', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.getSession.mockResolvedValue({ id: 'session-1', code: 'ABC123', name: 'Party', hostName: 'H', hasHost: true });
    mocks.getNowPlaying.mockResolvedValue({
      song: {
        id: 1,
        source: SongSource.Spotify,
        url: 'spotify:track:1',
        name: 'Track name',
        artists: 'Artist',
        albumArtUrl: null,
        durationMs: 1000,
      },
      paused: false,
      playerConnected: true,
    });
  });

  it('shows a guest what is playing and the queue, without any controls', async () => {
    mocks.getViewerRole.mockResolvedValue({ isHost: false, isAdmin: false });

    await renderPage();

    expect(screen.getByText('queue')).toBeVisible();
    expect(screen.queryByText('host player')).not.toBeInTheDocument();
    expect(screen.queryByText('admin controls')).not.toBeInTheDocument();
    expect(screen.queryByText('slideshow')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Maximize' })).not.toBeInTheDocument();
  });

  it('links an admin to the admin panel, and nobody else', async () => {
    mocks.getViewerRole.mockResolvedValue({ isHost: false, isAdmin: true });
    await renderPage();
    expect(await screen.findByRole('link', { name: 'Admin panel' })).toHaveAttribute('href', '/admin');
  });

  it('shows a guest no link to the admin panel', async () => {
    mocks.getViewerRole.mockResolvedValue({ isHost: false, isAdmin: false });
    await renderPage();
    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
  });

  it('gives the host the player', async () => {
    mocks.getViewerRole.mockResolvedValue({ isHost: true, isAdmin: false });

    await renderPage();

    expect(await screen.findByText('host player')).toBeVisible();
    expect(screen.queryByText('admin controls')).not.toBeInTheDocument();
  });

  it('gives an admin who is not the host the remote controls', async () => {
    mocks.getViewerRole.mockResolvedValue({ isHost: false, isAdmin: true });

    await renderPage();

    expect(await screen.findByText('admin controls')).toBeVisible();
    expect(screen.queryByText('host player')).not.toBeInTheDocument();
  });

  it('gives an admin who is also the host only the player', async () => {
    mocks.getViewerRole.mockResolvedValue({ isHost: true, isAdmin: true });

    await renderPage();

    expect(await screen.findByText('host player')).toBeVisible();
    expect(screen.queryByText('admin controls')).not.toBeInTheDocument();
  });

  describe('slideshow', () => {
    it.each([
      ['host', { isHost: true, isAdmin: false }],
      ['admin', { isHost: false, isAdmin: true }],
    ])('shows the %s the slideshow by default', async (_, role) => {
      mocks.getViewerRole.mockResolvedValue(role);

      await renderPage();

      expect(await screen.findByText('slideshow')).toBeVisible();
    });

    it('hides and shows the slideshow, and remembers the choice', async () => {
      mocks.getViewerRole.mockResolvedValue({ isHost: true, isAdmin: false });
      await renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'Hide slideshow' }));
      expect(screen.queryByText('slideshow')).not.toBeInTheDocument();
      expect(localStorage.getItem('partyking.slideshowOpen')).toBe('false');

      await userEvent.click(screen.getByRole('button', { name: 'Show slideshow' }));
      expect(await screen.findByText('slideshow')).toBeVisible();
    });

    it('starts hidden when it was hidden before', async () => {
      localStorage.setItem('partyking.slideshowOpen', 'false');
      mocks.getViewerRole.mockResolvedValue({ isHost: true, isAdmin: false });

      await renderPage();

      expect(await screen.findByRole('button', { name: 'Show slideshow' })).toBeVisible();
      expect(screen.queryByText('slideshow')).not.toBeInTheDocument();
    });

    it.each([
      ['host', { isHost: true, isAdmin: false }],
      ['admin', { isHost: false, isAdmin: true }],
    ])('lets the %s maximize it and leave again, keeping the page underneath', async (_, role) => {
      mocks.getViewerRole.mockResolvedValue(role);
      await renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'Maximize' }));

      expect(screen.getByRole('dialog', { name: 'Slideshow' })).toBeVisible();
      expect(screen.getByText('queue list')).toBeVisible();
      // The host's player must keep playing underneath
      if (role.isHost) expect(screen.getByText('host player')).toBeInTheDocument();

      await userEvent.click(screen.getByRole('button', { name: 'Exit full view' }));
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('leaves the maximized view on Escape', async () => {
      mocks.getViewerRole.mockResolvedValue({ isHost: true, isAdmin: false });
      await renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'Maximize' }));
      await userEvent.keyboard('{Escape}');

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  it('asks for the password instead of showing the session until it is entered', async () => {
    mocks.getSession.mockRejectedValueOnce({ status: 401, message: 'The session password is required' });
    mocks.getViewerRole.mockResolvedValue({ isHost: false, isAdmin: true });
    mocks.join.mockResolvedValue(undefined);

    render(
      <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
        <MemoryRouter initialEntries={['/s/session-1']}>
          <Routes>
            <Route path="/s/:sessionId" element={<MusicQueue />} />
          </Routes>
        </MemoryRouter>
      </SWRConfig>,
    );

    await userEvent.type(await screen.findByLabelText('Session password'), 'secret');
    expect(screen.queryByText('queue')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Join' }));

    expect(mocks.join).toHaveBeenCalledWith('session-1', 'secret');
    expect(await screen.findByText('queue')).toBeVisible();
    expect(screen.queryByLabelText('Session password')).not.toBeInTheDocument();
  });

  it.each([
    ['the host', { isHost: true, isAdmin: false }, true],
    ['an admin', { isHost: false, isAdmin: true }, true],
    ['a guest', { isHost: false, isAdmin: false }, false],
  ])('offers the QR code to %s: %s', async (_name, role, shown) => {
    mocks.getViewerRole.mockResolvedValue(role);

    await renderPage();

    if (shown) {
      expect(await screen.findByRole('button', { name: 'Show QR code' })).toBeVisible();
    } else {
      expect(screen.queryByRole('button', { name: 'Show QR code' })).not.toBeInTheDocument();
    }
  });

  describe('joining through a QR code', () => {
    const renderScanned = () =>
      render(
        <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
          <MemoryRouter initialEntries={['/s/session-1?join=tok']}>
            <Routes>
              <Route path="/s/:sessionId" element={<MusicQueue />} />
            </Routes>
          </MemoryRouter>
        </SWRConfig>,
      );

    it('unlocks the session with the scanned token instead of asking for the password', async () => {
      mocks.getSession.mockRejectedValueOnce({ status: 401, message: 'The session password is required' });
      mocks.getViewerRole.mockResolvedValue({ isHost: false, isAdmin: false });
      mocks.joinWithToken.mockResolvedValue(undefined);

      renderScanned();

      expect(await screen.findByText('queue')).toBeVisible();
      expect(mocks.joinWithToken).toHaveBeenCalledWith('session-1', 'tok');
      expect(screen.queryByLabelText('Session password')).not.toBeInTheDocument();
    });

    it('falls back to the password form when the token has expired', async () => {
      mocks.getSession.mockRejectedValue({ status: 401, message: 'The session password is required' });
      mocks.getViewerRole.mockResolvedValue({ isHost: false, isAdmin: false });
      mocks.joinWithToken.mockRejectedValue({ status: 401 });

      renderScanned();

      expect(await screen.findByText('This QR code has expired. Enter the password instead.')).toBeVisible();
      expect(screen.getByLabelText('Session password')).toBeVisible();
    });
  });
});
