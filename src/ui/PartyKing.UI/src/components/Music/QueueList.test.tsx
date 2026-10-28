import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { QueueList } from './QueueList';
import { SongSource } from '@/service/endpoints/queue/types';

const mocks = vi.hoisted(() => ({ getQueue: vi.fn(), remove: vi.fn() }));

vi.mock('@/service/endpoints/queue/queueApi', () => ({
  queueApi: { getQueue: mocks.getQueue, remove: mocks.remove },
}));

const song = (id: number, name: string) => ({
  id,
  source: SongSource.Spotify,
  url: `spotify:track:${id}`,
  name,
  artists: 'Artist',
  albumArtUrl: null,
  durationMs: 1000,
});

const renderList = (canRemove = false) =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <QueueList sessionId="session" canRemove={canRemove} />
    </SWRConfig>,
  );

describe('QueueList', () => {
  it('says so when the queue is empty', async () => {
    mocks.getQueue.mockResolvedValue([]);

    renderList();

    expect(await screen.findByText('The queue is empty')).toBeVisible();
  });

  it('lists the queued songs in order, without delete icons by default', async () => {
    mocks.getQueue.mockResolvedValue([song(1, 'First'), song(2, 'Second')]);

    renderList();

    expect(await screen.findByText('Artist - First')).toBeVisible();
    expect(screen.getByText('Artist - Second')).toBeVisible();
    expect(screen.queryByRole('button', { name: /remove/i })).not.toBeInTheDocument();
  });

  it('marks the songs queued from the fallback playlist', async () => {
    mocks.getQueue.mockResolvedValue([{ ...song(1, 'From list'), isFallback: true }, song(2, 'From guest')]);

    renderList();

    expect(await screen.findByText('Artist - From list')).toBeVisible();
    expect(screen.getAllByText('from playlist')).toHaveLength(1);
  });

  it('removes a song and shows the error when that fails', async () => {
    mocks.getQueue.mockResolvedValue([song(1, 'First')]);
    mocks.remove.mockRejectedValue({ message: 'The song is no longer waiting in the queue' });

    renderList(true);

    await userEvent.click(await screen.findByRole('button', { name: 'Remove First from the queue' }));

    expect(mocks.remove).toHaveBeenCalledWith('session', 1);
    expect(await screen.findByText('The song is no longer waiting in the queue')).toBeVisible();
  });
});
