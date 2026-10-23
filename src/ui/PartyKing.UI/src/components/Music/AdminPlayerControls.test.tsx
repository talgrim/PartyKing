import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminPlayerControls } from './AdminPlayerControls';
import { SongSource } from '@/service/endpoints/queue/types';

const mocks = vi.hoisted(() => ({
  sendPlayerCommand: vi.fn(),
  skipSong: vi.fn(),
}));

vi.mock('@/service/endpoints/admin/adminApi', () => ({ adminApi: mocks }));

const song = {
  id: 7,
  source: SongSource.Spotify,
  url: 'spotify:track:7',
  name: 'Track',
  artists: 'Artist',
  albumArtUrl: null,
  durationMs: 1000,
};

const renderControls = (nowPlaying: { paused: boolean; playerConnected: boolean; song: typeof song | null }) => {
  const onChanged = vi.fn();
  render(<AdminPlayerControls sessionId="session-1" nowPlaying={nowPlaying} onChanged={onChanged} />);
  return onChanged;
};

describe('AdminPlayerControls', () => {
  beforeEach(() => {
    mocks.sendPlayerCommand.mockReset().mockResolvedValue(undefined);
    mocks.skipSong.mockReset().mockResolvedValue(song);
  });

  it('disables both buttons while no player is connected', () => {
    renderControls({ song, paused: false, playerConnected: false });

    expect(screen.getByRole('button', { name: 'Pause' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next song' })).toBeDisabled();
  });

  it('asks the player to pause while the song plays', async () => {
    const onChanged = renderControls({ song, paused: false, playerConnected: true });

    await userEvent.click(screen.getByRole('button', { name: 'Pause' }));

    expect(mocks.sendPlayerCommand).toHaveBeenCalledWith('session-1', 'Pause');
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
  });

  it('asks the player to resume while the song is paused', async () => {
    renderControls({ song, paused: true, playerConnected: true });

    await userEvent.click(screen.getByRole('button', { name: 'Play' }));

    expect(mocks.sendPlayerCommand).toHaveBeenCalledWith('session-1', 'Resume');
  });

  it('skips from the song the admin sees', async () => {
    renderControls({ song, paused: false, playerConnected: true });

    await userEvent.click(screen.getByRole('button', { name: 'Next song' }));

    expect(mocks.skipSong).toHaveBeenCalledWith('session-1', 7);
  });

  it('keeps Play/Pause disabled when nothing is playing, but allows starting the next song', () => {
    renderControls({ song: null, paused: false, playerConnected: true });

    expect(screen.getByRole('button', { name: 'Pause' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next song' })).toBeEnabled();
  });

  it('explains a 409: the player went away since the page was loaded', async () => {
    mocks.skipSong.mockRejectedValue({ status: 409 });
    renderControls({ song, paused: false, playerConnected: true });

    await userEvent.click(screen.getByRole('button', { name: 'Next song' }));

    expect(await screen.findByText("The host's player is not connected.")).toBeVisible();
  });

  it('is not an error when the queue ran out', async () => {
    mocks.skipSong.mockRejectedValue({ status: 404 });
    renderControls({ song, paused: false, playerConnected: true });

    await userEvent.click(screen.getByRole('button', { name: 'Next song' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Next song' })).toBeEnabled());
    expect(screen.queryByText(/failed|not connected/)).not.toBeInTheDocument();
  });
});
