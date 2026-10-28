import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FallbackPlaylistForm } from './FallbackPlaylistForm';

const mocks = vi.hoisted(() => ({ getHostPlaylists: vi.fn(), setFallbackPlaylist: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: { getHostPlaylists: mocks.getHostPlaylists, setFallbackPlaylist: mocks.setFallbackPlaylist },
}));

const session = {
  id: 's1',
  code: 'ABC123',
  name: 'Party',
  hostName: 'Host',
  hasHost: true,
  fallbackPlaylistName: null,
  fallbackShuffle: false,
};

const renderForm = (overrides = {}, onChanged = vi.fn()) => {
  const user = userEvent.setup();
  render(<FallbackPlaylistForm session={{ ...session, ...overrides }} onChanged={onChanged} />);
  return { user, onChanged };
};

describe('FallbackPlaylistForm', () => {
  beforeEach(() => {
    mocks.getHostPlaylists.mockReset().mockResolvedValue([]);
    mocks.setFallbackPlaylist.mockReset().mockResolvedValue(undefined);
  });

  it('saves a pasted playlist link and tells the page', async () => {
    const { user, onChanged } = renderForm();

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    await user.type(screen.getByLabelText('Playlist link'), ' https://open.spotify.com/playlist/abc ');
    await user.click(screen.getByRole('checkbox', { name: 'Shuffle' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() =>
      expect(mocks.setFallbackPlaylist).toHaveBeenCalledWith('s1', 'https://open.spotify.com/playlist/abc', true),
    );
    expect(await screen.findByText('Fallback playlist saved')).toBeVisible();
    expect(onChanged).toHaveBeenCalled();
  });

  it('lets the host pick one of their own playlists, with its cover and track count', async () => {
    mocks.getHostPlaylists.mockResolvedValue([
      { uri: 'spotify:playlist:one', name: 'Warm up', imageUrl: 'https://img/one.jpg', trackCount: 20 },
      { uri: 'spotify:playlist:two', name: 'Closing', imageUrl: null, trackCount: 1 },
    ]);
    const { user } = renderForm();

    const list = await screen.findByRole('list', { name: 'Your playlists' });
    expect(list.querySelector('img')).toHaveAttribute('src', 'https://img/one.jpg');
    expect(screen.getByText('20 tracks')).toBeVisible();
    expect(screen.getByText('1 track')).toBeVisible();

    await user.click(screen.getByRole('button', { name: /Warm up/ }));

    expect(screen.getByRole('button', { name: /Warm up/ })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByLabelText('Or paste a playlist link')).toHaveValue('spotify:playlist:one');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(mocks.setFallbackPlaylist).toHaveBeenCalledWith('s1', 'spotify:playlist:one', false));
  });

  it('shows the cover of the current playlist', async () => {
    mocks.getHostPlaylists.mockResolvedValue([
      { uri: 'spotify:playlist:one', name: 'Party mix', imageUrl: 'https://img/one.jpg', trackCount: 3 },
    ]);

    renderForm({ fallbackPlaylistName: 'Party mix' });

    await waitFor(() =>
      expect(document.querySelector('img[src="https://img/one.jpg"]')).toBeInTheDocument(),
    );
  });

  it('shows why the playlist was refused', async () => {
    mocks.setFallbackPlaylist.mockRejectedValue({ message: 'Only playlists the host owns can be used' });
    const { user } = renderForm();

    await user.type(screen.getByLabelText('Playlist link'), 'spotify:playlist:abc');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Only playlists the host owns can be used')).toBeVisible();
  });

  it('shows the current playlist and removes it', async () => {
    const { user, onChanged } = renderForm({ fallbackPlaylistName: 'Party mix' });

    expect(screen.getByText(/Party mix/)).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(mocks.setFallbackPlaylist).toHaveBeenCalledWith('s1', null, false));
    expect(onChanged).toHaveBeenCalled();
  });

  it('hides the picker for an admin who is not the host', async () => {
    mocks.getHostPlaylists.mockRejectedValue({ status: 403, message: 'Forbidden' });

    renderForm();

    await waitFor(() => expect(mocks.getHostPlaylists).toHaveBeenCalled());
    expect(screen.queryByLabelText('Your playlists')).not.toBeInTheDocument();
    expect(screen.queryByText('Forbidden')).not.toBeInTheDocument();
  });

  it('asks the host to log in again when Spotify refuses the list', async () => {
    mocks.getHostPlaylists.mockRejectedValue({ status: 409, message: 'Log in as the host again' });

    renderForm();

    expect(await screen.findByText('Log in as the host again')).toBeVisible();
  });
});
