import { act, render, screen, waitFor } from '@testing-library/react';
import { useEffect } from 'react';
import { ScopedMutator, SWRConfig, useSWRConfig } from 'swr';
import { HostPlayer } from './HostPlayer';
import { SongSource } from '@/service/endpoints/queue/types';
import { HostPlayerPushHandlers, nowPlayingKey } from '@/service/realtime/sessionMusicEvents';

const mocks = vi.hoisted(() => ({
  next: vi.fn(),
  getNowPlaying: vi.fn(),
  reportState: vi.fn(),
}));

/** Captures the mutate bound to this SWRConfig provider (the global one cannot see it). */
const MutateCapture = ({ onReady }: { onReady: (mutate: ScopedMutator) => void }) => {
  const { mutate } = useSWRConfig();
  useEffect(() => onReady(mutate), [mutate, onReady]);
  return null;
};

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
vi.mock('./SpotifyPlayer', () => ({ SpotifyPlayer: () => null }));
vi.mock('./YoutubePlayer', () => ({
  YoutubePlayer: ({ songUrl, paused, volume, onError }: { songUrl: string; paused: boolean; volume: number; onError: (code: number) => void }) => (
    <div>
      <span>{`volume ${volume}`}</span>
      <span>{`video ${songUrl} ${paused ? 'paused' : 'playing'}`}</span>
      <button onClick={() => onError(150)}>fail video</button>
    </div>
  ),
}));

const song = (id: number, name: string) => ({
  id,
  source: SongSource.Youtube,
  url: `https://www.youtube.com/watch?v=${id}`,
  name,
  artists: null,
  albumArtUrl: null,
  durationMs: null,
});

const nowPlaying = (current: ReturnType<typeof song> | null) => ({
  song: current,
  paused: false,
  playerConnected: true,
});

const startMusic = async () => {
  const start = await screen.findByRole('button', { name: 'Start the music' });
  await waitFor(() => expect(start).toBeEnabled());
  await act(async () => start.click());
};

const renderPlayer = async (isAdmin = false, pushHandlers?: { current: HostPlayerPushHandlers }) => {
  let mutate!: ScopedMutator;
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MutateCapture onReady={(m) => { mutate = m; }} />
      <HostPlayer sessionId="session-1" isAdmin={isAdmin} pushHandlers={pushHandlers} />
    </SWRConfig>,
  );
  await startMusic();
  await screen.findByText('fail video');
  return { mutate: () => mutate };
};

describe('HostPlayer', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.next.mockReset();
    mocks.next.mockResolvedValueOnce(song(1, 'Blocked song'));
    mocks.getNowPlaying.mockReset();
    mocks.getNowPlaying.mockResolvedValue(nowPlaying(null));
    mocks.reportState.mockReset();
    mocks.reportState.mockResolvedValue({ command: null });
  });

  it('skips a video that YouTube refuses to play and tells the host why', async () => {
    mocks.next.mockResolvedValueOnce(song(2, 'Good song'));
    await renderPlayer();

    await act(async () => screen.getByText('fail video').click());

    expect(await screen.findByText(/Skipped "Blocked song": The owner of the video does not allow/)).toBeVisible();
    expect(mocks.next).toHaveBeenCalledTimes(2);
  });

  it('stops skipping after repeated YouTube failures and resumes on "Next song"', async () => {
    mocks.next.mockResolvedValueOnce(song(2, 'Second blocked song')).mockResolvedValueOnce(song(3, 'Good song'));
    await renderPlayer();

    await act(async () => screen.getByText('fail video').click());
    await screen.findByText(/Skipped "Blocked song"/);
    await act(async () => screen.getByText('fail video').click());

    expect(await screen.findByText(/YouTube refused 2 videos in a row/)).toBeVisible();
    expect(mocks.next).toHaveBeenCalledTimes(2);

    await act(async () => screen.getByRole('button', { name: 'Next song' }).click());
    await waitFor(() => expect(mocks.next).toHaveBeenCalledTimes(3));
  });

  it('shows the error log to the admin', async () => {
    mocks.next.mockRejectedValueOnce({ status: 404 });
    await renderPlayer(true);

    await act(async () => screen.getByText('fail video').click());

    const log = await screen.findByRole('region', { name: 'Error log' });
    expect(log).toHaveTextContent(/YouTube error 150 on "Blocked song"/);
  });

  it('hides the error log from everyone else', async () => {
    mocks.next.mockRejectedValueOnce({ status: 404 });
    await renderPlayer();

    await act(async () => screen.getByText('fail video').click());

    await waitFor(() => expect(mocks.next).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole('region', { name: 'Error log' })).not.toBeInTheDocument();
  });

  it('tells the server which song it plays when asking for the next one', async () => {
    mocks.next.mockResolvedValueOnce(song(2, 'Good song'));
    await renderPlayer();
    expect(mocks.next).toHaveBeenLastCalledWith('session-1', undefined);

    await act(async () => screen.getByRole('button', { name: 'Next song' }).click());

    await waitFor(() => expect(mocks.next).toHaveBeenCalledTimes(2));
    expect(mocks.next).toHaveBeenLastCalledWith('session-1', 1);
  });

  it('reports the song and the pause state to the server', async () => {
    await renderPlayer();

    await waitFor(() =>
      expect(mocks.reportState).toHaveBeenCalledWith('session-1', { songId: 1, paused: false, positionMs: 0, seeked: false }),
    );

    await act(async () => screen.getByRole('button', { name: 'Pause' }).click());

    await waitFor(() =>
      expect(mocks.reportState).toHaveBeenLastCalledWith('session-1', { songId: 1, paused: true, positionMs: 0, seeked: false }),
    );
  });

  it('tells the host why the fallback playlist could not add a song', async () => {
    mocks.reportState.mockResolvedValue({
      command: null,
      fallbackError: 'Fallback playlist: the playlist has no playable tracks',
    });

    await renderPlayer();

    expect(await screen.findByText('Fallback playlist: the playlist has no playable tracks')).toBeVisible();
  });

  it('pauses when an admin sends the pause command', async () => {
    mocks.reportState.mockResolvedValueOnce({ command: null }).mockResolvedValue({ command: 'Pause' });
    await renderPlayer();

    expect(await screen.findByText(/video .* paused/)).toBeVisible();
    expect(screen.getByRole('button', { name: 'Play' })).toBeInTheDocument();
  });

  it('pauses at once when the server pushes the pause command', async () => {
    const push = { current: {} as HostPlayerPushHandlers };
    await renderPlayer(false, push);
    expect(screen.getByText(/video .* playing/)).toBeVisible();

    await act(async () => push.current.onCommand?.('Pause'));

    expect(await screen.findByText(/video .* paused/)).toBeVisible();
  });

  it('ignores a pushed command that does not change anything', async () => {
    const push = { current: {} as HostPlayerPushHandlers };
    await renderPlayer(false, push);

    await act(async () => push.current.onCommand?.('Resume'));

    expect(screen.getByText(/video .* playing/)).toBeVisible();
  });

  it('shows and clears a fallback error the server pushes', async () => {
    const push = { current: {} as HostPlayerPushHandlers };
    await renderPlayer(false, push);

    await act(async () => push.current.onFallbackError?.('Fallback playlist: Spotify could not provide the tracks'));
    expect(await screen.findByText('Fallback playlist: Spotify could not provide the tracks')).toBeVisible();

    await act(async () => push.current.onFallbackError?.(null));
    await waitFor(() =>
      expect(screen.queryByText('Fallback playlist: Spotify could not provide the tracks')).not.toBeInTheDocument(),
    );
  });

  it('reports again after the connection to the server was restored', async () => {
    const push = { current: {} as HostPlayerPushHandlers };
    await renderPlayer(false, push);
    await waitFor(() => expect(mocks.reportState).toHaveBeenCalled());
    const reports = mocks.reportState.mock.calls.length;

    await act(async () => push.current.onReconnected?.());

    await waitFor(() => expect(mocks.reportState.mock.calls.length).toBeGreaterThan(reports));
  });

  it('follows the server when an admin skips to another song', async () => {
    const { mutate } = await renderPlayer();
    expect(screen.getByText(/watch\?v=1 playing/)).toBeVisible();

    await act(async () =>
      mutate()(nowPlayingKey('session-1'), nowPlaying(song(2, 'Skipped to')), { revalidate: false }),
    );

    expect(await screen.findByText(/watch\?v=2 playing/)).toBeVisible();
    // The admin's skip is not the player's own advance
    expect(mocks.next).toHaveBeenCalledTimes(1);
  });

  it('stops when an admin skips past the last song', async () => {
    mocks.getNowPlaying.mockResolvedValue(nowPlaying(song(1, 'Blocked song')));
    const { mutate } = await renderPlayer();

    await act(async () => mutate()(nowPlayingKey('session-1'), nowPlaying(null), { revalidate: false }));

    await waitFor(() => expect(screen.queryByText(/video /)).not.toBeInTheDocument());
  });

  it('plays at 50% and raises the volume by 10% per click', async () => {
    await renderPlayer();
    expect(screen.getByText('volume 0.5')).toBeVisible();
    expect(screen.getByText('50%')).toBeVisible();

    await act(async () => screen.getByRole('button', { name: 'Volume up' }).click());

    expect(screen.getByText('volume 0.6')).toBeVisible();
    expect(screen.getByText('60%')).toBeVisible();
  });

  it('remembers the volume after a reload', async () => {
    localStorage.setItem('partyking.volume', '0.8');
    await renderPlayer();

    expect(screen.getByText('volume 0.8')).toBeVisible();
    expect(screen.getByText('80%')).toBeVisible();
  });
});
