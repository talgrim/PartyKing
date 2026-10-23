import { act, render, screen, waitFor } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { HostPlayer } from './HostPlayer';
import { SongSource } from '@/service/endpoints/queue/types';

const mocks = vi.hoisted(() => ({
  next: vi.fn(),
  getNowPlaying: vi.fn(),
  reportState: vi.fn(),
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
vi.mock('./SpotifyPlayer', () => ({ SpotifyPlayer: () => null }));
vi.mock('./YoutubePlayer', () => ({
  YoutubePlayer: ({ songUrl, paused, onError }: { songUrl: string; paused: boolean; onError: (code: number) => void }) => (
    <div>
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
  act(() => start.click());
};

const renderPlayer = async (isAdmin = false) => {
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <HostPlayer sessionId="session-1" isAdmin={isAdmin} />
    </SWRConfig>,
  );
  await startMusic();
  await screen.findByText('fail video');
};

describe('HostPlayer', () => {
  beforeEach(() => {
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

    act(() => screen.getByText('fail video').click());

    expect(await screen.findByText(/Skipped "Blocked song": The owner of the video does not allow/)).toBeVisible();
    expect(mocks.next).toHaveBeenCalledTimes(2);
  });

  it('stops skipping after repeated YouTube failures and resumes on "Next song"', async () => {
    mocks.next.mockResolvedValueOnce(song(2, 'Second blocked song')).mockResolvedValueOnce(song(3, 'Good song'));
    await renderPlayer();

    act(() => screen.getByText('fail video').click());
    await screen.findByText(/Skipped "Blocked song"/);
    act(() => screen.getByText('fail video').click());

    expect(await screen.findByText(/YouTube refused 2 videos in a row/)).toBeVisible();
    expect(mocks.next).toHaveBeenCalledTimes(2);

    act(() => screen.getByRole('button', { name: 'Next song' }).click());
    await waitFor(() => expect(mocks.next).toHaveBeenCalledTimes(3));
  });

  it('shows the error log to the admin', async () => {
    mocks.next.mockRejectedValueOnce({ status: 404 });
    await renderPlayer(true);

    act(() => screen.getByText('fail video').click());

    const log = await screen.findByRole('region', { name: 'Error log' });
    expect(log).toHaveTextContent(/YouTube error 150 on "Blocked song"/);
  });

  it('hides the error log from everyone else', async () => {
    mocks.next.mockRejectedValueOnce({ status: 404 });
    await renderPlayer();

    act(() => screen.getByText('fail video').click());

    await waitFor(() => expect(mocks.next).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole('region', { name: 'Error log' })).not.toBeInTheDocument();
  });

  it('tells the server which song it plays when asking for the next one', async () => {
    mocks.next.mockResolvedValueOnce(song(2, 'Good song'));
    await renderPlayer();
    expect(mocks.next).toHaveBeenLastCalledWith('session-1', undefined);

    act(() => screen.getByRole('button', { name: 'Next song' }).click());

    await waitFor(() => expect(mocks.next).toHaveBeenCalledTimes(2));
    expect(mocks.next).toHaveBeenLastCalledWith('session-1', 1);
  });

  it('reports the song and the pause state to the server', async () => {
    await renderPlayer();

    await waitFor(() =>
      expect(mocks.reportState).toHaveBeenCalledWith('session-1', { songId: 1, paused: false }),
    );

    act(() => screen.getByRole('button', { name: 'Pause' }).click());

    await waitFor(() =>
      expect(mocks.reportState).toHaveBeenLastCalledWith('session-1', { songId: 1, paused: true }),
    );
  });

  it('pauses when an admin sends the pause command', async () => {
    mocks.reportState.mockResolvedValueOnce({ command: null }).mockResolvedValue({ command: 'Pause' });
    await renderPlayer();

    expect(await screen.findByText(/video .* paused/)).toBeVisible();
    expect(screen.getByRole('button', { name: 'Play' })).toBeInTheDocument();
  });

  it('follows the server when an admin skips to another song', async () => {
    await renderPlayer();
    expect(screen.getByText(/watch\?v=1 playing/)).toBeVisible();

    mocks.getNowPlaying.mockResolvedValue(nowPlaying(song(2, 'Skipped to')));

    expect(await screen.findByText(/watch\?v=2 playing/, undefined, { timeout: 4000 })).toBeVisible();
    // The admin's skip is not the player's own advance
    expect(mocks.next).toHaveBeenCalledTimes(1);
  });

  it('stops when an admin skips past the last song', async () => {
    await renderPlayer();

    mocks.getNowPlaying.mockResolvedValue(nowPlaying(null));
    mocks.getNowPlaying.mockResolvedValueOnce(nowPlaying(song(1, 'Blocked song')));

    await waitFor(() => expect(screen.queryByText(/video /)).not.toBeInTheDocument(), { timeout: 4000 });
  });
});
