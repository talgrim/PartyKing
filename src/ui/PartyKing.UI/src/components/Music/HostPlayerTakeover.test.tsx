import { act, render, screen, waitFor } from '@testing-library/react';
import { useEffect } from 'react';
import { SWRConfig } from 'swr';
import { HostPlayer } from './HostPlayer';
import { SongSource } from '@/service/endpoints/queue/types';

const mocks = vi.hoisted(() => ({
  next: vi.fn(),
  getNowPlaying: vi.fn(),
  reportState: vi.fn(),
  getPlaybackState: vi.fn(),
  takeoverProp: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: {} }));
vi.mock('@/service/endpoints/queue/queueApi', () => ({
  queueApi: {
    getSpotifyToken: vi.fn().mockResolvedValue('token'),
    getQueue: vi.fn().mockResolvedValue([]),
    next: mocks.next,
  },
}));
vi.mock('@/service/endpoints/player/playerApi', () => ({
  playerApi: { getNowPlaying: mocks.getNowPlaying, reportState: mocks.reportState },
}));
vi.mock('@/service/endpoints/spotify/spotifyPlaybackApi', () => ({
  getPlaybackState: mocks.getPlaybackState,
}));
vi.mock('./YoutubePlayer', () => ({ YoutubePlayer: () => null }));
vi.mock('./SpotifyPlayer', () => ({
  SpotifyPlayer: ({
    onDeviceChange,
    takeover,
  }: {
    onDeviceChange: (id: string) => void;
    takeover: unknown;
  }) => {
    useEffect(() => onDeviceChange('web-device'), [onDeviceChange]);
    mocks.takeoverProp(takeover);
    return null;
  },
}));

const song = {
  id: 1,
  source: SongSource.Spotify,
  url: 'spotify:track:current',
  name: 'Current',
  artists: 'Artist',
  albumArtUrl: null,
  durationMs: 200_000,
};

const elsewhere = (overrides = {}) => ({
  deviceId: 'phone',
  uri: song.url,
  isPlaying: false,
  progressMs: 42_000,
  timestamp: Date.now(),
  ...overrides,
});

const renderPlayer = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <HostPlayer sessionId="session-1" isAdmin={false} />
    </SWRConfig>,
  );

describe('HostPlayer taking over from another device', () => {
  beforeEach(() => {
    Object.values(mocks).forEach((mock) => mock.mockReset());
    mocks.next.mockResolvedValue(song);
    mocks.getNowPlaying.mockResolvedValue({ song, paused: false, playerConnected: false });
    mocks.reportState.mockResolvedValue({ command: null });
  });

  it('offers to play here and continues from the other device\'s position', async () => {
    mocks.getPlaybackState.mockResolvedValue(elsewhere());
    renderPlayer();

    const button = await screen.findByRole('button', { name: 'Play here' });
    await waitFor(() => expect(button).toBeEnabled());
    act(() => button.click());

    await waitFor(() =>
      expect(mocks.takeoverProp).toHaveBeenLastCalledWith({ uri: song.url, positionMs: 42_000 }),
    );
    expect(mocks.next).toHaveBeenCalled();
  });

  it('offers the normal button when the song plays on this device', async () => {
    mocks.getPlaybackState.mockResolvedValue(elsewhere({ deviceId: 'web-device' }));
    renderPlayer();

    expect(await screen.findByRole('button', { name: 'Start the music' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Play here' })).not.toBeInTheDocument();
  });

  it('offers the normal button when Spotify refuses to tell, e.g. the scope is missing', async () => {
    mocks.getPlaybackState.mockRejectedValue({ status: 403 });
    renderPlayer();

    const button = await screen.findByRole('button', { name: 'Start the music' });
    await waitFor(() => expect(button).toBeEnabled());
    expect(screen.queryByRole('button', { name: 'Play here' })).not.toBeInTheDocument();
  });
});
