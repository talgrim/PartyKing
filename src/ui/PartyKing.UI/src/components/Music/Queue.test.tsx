import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { Queue } from './Queue';
import { SongSource } from '@/service/endpoints/queue/types';

const mocks = vi.hoisted(() => ({
  getQueue: vi.fn(),
  enqueue: vi.fn(),
  searchSpotify: vi.fn(),
  remove: vi.fn(),
}));

vi.mock('@/service/endpoints/queue/queueApi', () => ({
  queueApi: mocks,
  looksLikeSongLink: (input: string) => input.startsWith('http') || input.startsWith('spotify:'),
}));

const track = (n: number) => ({
  uri: `spotify:track:${n}`,
  name: `Track ${n}`,
  artists: 'Artist',
  albumArtUrl: null,
  durationMs: 1000,
});

const queued = (n: number) => ({
  id: n,
  source: SongSource.Spotify,
  url: `spotify:track:${n}`,
  name: `Track ${n}`,
  artists: 'Artist',
  albumArtUrl: null,
  durationMs: 1000,
});

const renderQueue = async () => {
  const user = userEvent.setup();
  render(
    <SWRConfig value={{ provider: () => new Map() }}>
      <Queue sessionId="session" />
    </SWRConfig>,
  );
  await user.type(screen.getByPlaceholderText(/search spotify/i), 'party');
  await user.click(screen.getByRole('button', { name: 'Search' }));
  await screen.findByText('Track 1');
  return user;
};

describe('Queue', () => {
  let pending: ReturnType<typeof queued>[];

  beforeEach(() => {
    pending = [];
    mocks.getQueue.mockImplementation(async () => pending);
    mocks.searchSpotify.mockResolvedValue([track(1), track(2), track(3)]);
    mocks.enqueue.mockImplementation(async (_: string, uri: string) => {
      pending = [...pending, queued(Number(uri.split(':')[2]))];
      return pending[pending.length - 1];
    });
  });

  afterEach(() => vi.clearAllMocks());

  it('keeps the results open so several songs can be added from one search', async () => {
    const user = await renderQueue();

    await user.click(screen.getAllByRole('button', { name: 'Add' })[0]);
    await waitFor(() => expect(screen.getByRole('button', { name: 'In queue' })).toBeDisabled());
    await user.click(screen.getAllByRole('button', { name: 'Add' })[0]);

    await waitFor(() => expect(screen.getAllByRole('button', { name: 'In queue' })).toHaveLength(2));
    expect(mocks.enqueue).toHaveBeenCalledTimes(2);
    expect(mocks.enqueue).toHaveBeenNthCalledWith(1, 'session', 'spotify:track:1');
    expect(mocks.enqueue).toHaveBeenNthCalledWith(2, 'session', 'spotify:track:2');
    expect(screen.getByText('Track 3')).toBeInTheDocument();
  });

  it('turns a row back into an Add button once its song leaves the queue', async () => {
    const user = await renderQueue();

    await user.click(screen.getAllByRole('button', { name: 'Add' })[0]);
    const inQueue = await screen.findByRole('button', { name: 'In queue' });

    pending = [];

    // the queue is polled every 5 seconds
    await waitFor(() => expect(inQueue).toHaveTextContent('Add'), { timeout: 7000 });
    expect(inQueue).toBeEnabled();
  }, 10_000);

  it('shows the error on the failed row and lets it be retried', async () => {
    mocks.enqueue.mockRejectedValueOnce({ message: 'Session has no host yet' });
    const user = await renderQueue();

    await user.click(screen.getAllByRole('button', { name: 'Add' })[0]);

    expect(await screen.findByText('Session has no host yet')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Add' })).toHaveLength(3);

    await user.click(screen.getAllByRole('button', { name: 'Add' })[0]);
    await screen.findByRole('button', { name: 'In queue' });
    expect(screen.queryByText('Session has no host yet')).not.toBeInTheDocument();
  });

  it('closes the dropdown on an outside press and reopens it from the search field without clearing the text', async () => {
    const user = await renderQueue();

    await user.click(screen.getByRole('heading', { name: 'Up next' }));
    expect(screen.queryByText('Track 1')).not.toBeInTheDocument();
    expect(screen.getByPlaceholderText(/search spotify/i)).toHaveValue('party');

    await user.click(screen.getByPlaceholderText(/search spotify/i));
    expect(await screen.findByText('Track 1')).toBeInTheDocument();
  });

  it('keeps the dropdown open when pressing inside it', async () => {
    const user = await renderQueue();

    await user.click(screen.getByText('Track 2'));

    expect(screen.getByText('Track 1')).toBeInTheDocument();
  });

  it('closes the dropdown on Escape', async () => {
    const user = await renderQueue();

    await user.keyboard('{Escape}');

    expect(screen.queryByText('Track 1')).not.toBeInTheDocument();
  });

  it('clears the search field and the results with the clear button', async () => {
    const user = await renderQueue();

    await user.click(screen.getByRole('button', { name: 'Clear search' }));

    expect(screen.getByPlaceholderText(/search spotify/i)).toHaveValue('');
    expect(screen.queryByText('Track 1')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear search' })).not.toBeInTheDocument();
  });

  describe('removing songs', () => {
    const renderPending = (canRemove: boolean) => {
      pending = [queued(1), queued(2)];
      render(
        <SWRConfig value={{ provider: () => new Map() }}>
          <Queue sessionId="session" canRemove={canRemove} />
        </SWRConfig>,
      );
    };

    it('shows no delete icon without permission', async () => {
      renderPending(false);

      await screen.findByText('Artist - Track 1');
      expect(screen.queryByRole('button', { name: /remove/i })).not.toBeInTheDocument();
    });

    it('colors the delete icon red', async () => {
      renderPending(true);

      expect(await screen.findByRole('button', { name: 'Remove Track 1 from the queue' })).toHaveClass('text-red-500');
    });

    it('removes the song and refreshes the queue', async () => {
      const user = userEvent.setup();
      mocks.remove.mockImplementation(async (_: string, id: number) => {
        pending = pending.filter((song) => song.id !== id);
      });
      renderPending(true);

      await user.click(await screen.findByRole('button', { name: 'Remove Track 1 from the queue' }));

      expect(mocks.remove).toHaveBeenCalledWith('session', 1);
      await waitFor(() => expect(screen.queryByText('Artist - Track 1')).not.toBeInTheDocument());
      expect(screen.getByText('Artist - Track 2')).toBeInTheDocument();
    });

    it('shows the error when the song cannot be removed', async () => {
      const user = userEvent.setup();
      mocks.remove.mockRejectedValue({ status: 409, message: 'The song is no longer waiting in the queue' });
      renderPending(true);

      await user.click(await screen.findByRole('button', { name: 'Remove Track 1 from the queue' }));

      expect(await screen.findByText('The song is no longer waiting in the queue')).toBeInTheDocument();
    });
  });
});
