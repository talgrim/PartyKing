import { act, render, screen, waitFor } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { Music } from './Music';
import { SongSource } from '@/service/endpoints/queue/types';

const mocks = vi.hoisted(() => ({
  me: vi.fn(),
  next: vi.fn(),
}));

vi.mock('@/service/endpoints/admin/adminApi', () => ({ adminApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: {} }));
vi.mock('@/service/endpoints/queue/queueApi', () => ({
  queueApi: {
    getSpotifyToken: vi.fn().mockResolvedValue('token'),
    getQueue: vi.fn().mockResolvedValue([]),
    next: mocks.next,
  },
}));
vi.mock('@/components/Music/Queue', () => ({ Queue: () => null }));
vi.mock('@/components/Music/SpotifyPlayer', () => ({ SpotifyPlayer: () => null }));
vi.mock('@/components/Music/YoutubePlayer', () => ({
  YoutubePlayer: ({ onError }: { onError: (code: number) => void }) => (
    <button onClick={() => onError(150)}>fail video</button>
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

const renderMusic = async () => {
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/music']}>
        <Routes>
          <Route path="/s/:sessionId/music" element={<Music />} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );
  const start = await screen.findByRole('button', { name: 'Start the music' });
  await waitFor(() => expect(start).toBeEnabled());
  act(() => start.click());
  await screen.findByText('fail video');
};

describe('Music', () => {
  beforeEach(() => {
    mocks.next.mockReset();
    mocks.next.mockResolvedValueOnce(song(1, 'Blocked song'));
  });

  it('skips a video that YouTube refuses to play and tells the host why', async () => {
    mocks.me.mockRejectedValue({ status: 401 });
    mocks.next.mockResolvedValueOnce(song(2, 'Good song'));
    await renderMusic();

    act(() => screen.getByText('fail video').click());

    expect(await screen.findByText(/Skipped "Blocked song": The owner of the video does not allow/)).toBeVisible();
    expect(mocks.next).toHaveBeenCalledTimes(2);
  });

  it('stops skipping after repeated YouTube failures and resumes on "Next song"', async () => {
    mocks.me.mockRejectedValue({ status: 401 });
    mocks.next.mockResolvedValueOnce(song(2, 'Second blocked song')).mockResolvedValueOnce(song(3, 'Good song'));
    await renderMusic();

    act(() => screen.getByText('fail video').click());
    await screen.findByText(/Skipped "Blocked song"/);
    act(() => screen.getByText('fail video').click());

    expect(await screen.findByText(/YouTube refused 2 videos in a row/)).toBeVisible();
    expect(mocks.next).toHaveBeenCalledTimes(2);

    act(() => screen.getByRole('button', { name: 'Next song' }).click());
    await waitFor(() => expect(mocks.next).toHaveBeenCalledTimes(3));
  });

  it('shows the error log to the admin', async () => {
    mocks.me.mockResolvedValue(undefined);
    mocks.next.mockRejectedValueOnce({ status: 404 });
    await renderMusic();

    act(() => screen.getByText('fail video').click());

    const log = await screen.findByRole('region', { name: 'Error log' });
    expect(log).toHaveTextContent(/YouTube error 150 on "Blocked song"/);
  });

  it('hides the error log from everyone else', async () => {
    mocks.me.mockRejectedValue({ status: 401 });
    mocks.next.mockRejectedValueOnce({ status: 404 });
    await renderMusic();

    act(() => screen.getByText('fail video').click());

    await waitFor(() => expect(mocks.next).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole('region', { name: 'Error log' })).not.toBeInTheDocument();
  });
});
