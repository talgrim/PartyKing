import { render, screen } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { MusicQueue } from './MusicQueue';
import { SongSource } from '@/service/endpoints/queue/types';

const mocks = vi.hoisted(() => ({
  getSession: vi.fn(),
  getViewerRole: vi.fn(),
  getNowPlaying: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: { getSession: mocks.getSession, getViewerRole: mocks.getViewerRole },
}));
vi.mock('@/service/endpoints/player/playerApi', () => ({ playerApi: { getNowPlaying: mocks.getNowPlaying } }));
vi.mock('@/components/Music/Queue', () => ({ Queue: () => <div>queue</div> }));
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
});
