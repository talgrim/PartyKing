import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { FallbackPlaylistForm } from './FallbackPlaylistForm';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';

const mocks = vi.hoisted(() => ({
  getHostPlaylists: vi.fn(),
  setFallbackPlaylist: vi.fn(),
  setFallbackSavedPlaylist: vi.fn(),
  list: vi.fn(),
  me: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: {
    getHostPlaylists: mocks.getHostPlaylists,
    setFallbackPlaylist: mocks.setFallbackPlaylist,
    setFallbackSavedPlaylist: mocks.setFallbackSavedPlaylist,
  },
}));
vi.mock('@/service/endpoints/playlists/playlistsApi', () => ({ playlistsApi: { list: mocks.list } }));
vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));

const friday = { id: 'p1', name: 'Friday', createdAt: '2026-11-09T12:00:00Z', songCount: 3 };
const warmUp = { id: 'p2', name: 'Warm up', createdAt: '2026-11-08T12:00:00Z', songCount: 1 };

const signedIn = () => {
  localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
  mocks.me.mockResolvedValue({ id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' });
};

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
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <FallbackPlaylistForm session={{ ...session, ...overrides }} onChanged={onChanged} />
    </SWRConfig>,
  );
  return { user, onChanged };
};

describe('FallbackPlaylistForm', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.getHostPlaylists.mockReset().mockResolvedValue([]);
    mocks.setFallbackPlaylist.mockReset().mockResolvedValue(undefined);
    mocks.setFallbackSavedPlaylist.mockReset().mockResolvedValue(undefined);
    mocks.list.mockReset().mockResolvedValue([friday, warmUp]);
    mocks.me.mockReset();
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

    const list = await screen.findByRole('list', { name: 'Your Spotify playlists' });
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

  it('does not take the cover of a Spotify playlist for a saved playlist of the same name', async () => {
    mocks.getHostPlaylists.mockResolvedValue([
      { uri: 'spotify:playlist:one', name: 'Party mix', imageUrl: 'https://img/one.jpg', trackCount: 3 },
    ]);

    renderForm({ fallbackPlaylistName: 'Party mix', fallbackSavedPlaylistId: 'p1' });

    await screen.findByRole('list', { name: 'Your Spotify playlists' });
    expect(screen.getByText('Party mix', { selector: 'p' })).toBeVisible();
    expect(document.querySelectorAll('img[src="https://img/one.jpg"]')).toHaveLength(1);
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
    expect(screen.queryByLabelText('Your Spotify playlists')).not.toBeInTheDocument();
    expect(screen.queryByText('Forbidden')).not.toBeInTheDocument();
  });

  it('asks the host to log in again when Spotify refuses the list', async () => {
    mocks.getHostPlaylists.mockRejectedValue({ status: 409, message: 'Log in as the host again' });

    renderForm();

    expect(await screen.findByText('Log in as the host again')).toBeVisible();
  });

  it('makes a saved playlist the fallback with the one Shuffle and the one Save, and tells the page', async () => {
    signedIn();
    const { user, onChanged } = renderForm();

    await user.click(await screen.findByRole('button', { name: /Warm up/ }));
    expect(screen.getByRole('button', { name: /Warm up/ })).toHaveAttribute('aria-pressed', 'true');
    await user.click(screen.getByRole('checkbox', { name: 'Shuffle' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(mocks.setFallbackSavedPlaylist).toHaveBeenCalledWith('s1', 'p2', true));
    expect(mocks.setFallbackPlaylist).not.toHaveBeenCalled();
    expect(await screen.findByText('Fallback playlist saved')).toBeVisible();
    expect(onChanged).toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /Warm up/ })).toHaveAttribute('aria-pressed', 'false');
  });

  it('chooses one playlist at a time, whether it is saved, one of Spotify or a pasted link', async () => {
    signedIn();
    mocks.getHostPlaylists.mockResolvedValue([
      { uri: 'spotify:playlist:one', name: 'Warm-up mix', imageUrl: null, trackCount: 20 },
    ]);
    const { user } = renderForm();

    await user.click(await screen.findByRole('button', { name: /Friday/ }));
    await user.click(screen.getByRole('button', { name: /Warm-up mix/ }));

    expect(screen.getByRole('button', { name: /Friday/ })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: /Warm-up mix/ })).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', { name: /Friday/ }));

    expect(screen.getByRole('button', { name: /Warm-up mix/ })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByLabelText('Or paste a playlist link')).toHaveValue('');

    await user.type(screen.getByLabelText('Or paste a playlist link'), 'spotify:playlist:abc');

    expect(screen.getByRole('button', { name: /Friday/ })).toHaveAttribute('aria-pressed', 'false');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(mocks.setFallbackPlaylist).toHaveBeenCalledWith('s1', 'spotify:playlist:abc', false));
    expect(mocks.setFallbackSavedPlaylist).not.toHaveBeenCalled();
  });

  it('shows why a saved playlist was refused, and keeps the choice', async () => {
    signedIn();
    mocks.setFallbackSavedPlaylist.mockRejectedValue({ status: 404, message: 'The playlist was not found' });
    const { user, onChanged } = renderForm();

    await user.click(await screen.findByRole('button', { name: /Friday/ }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('The playlist was not found')).toBeVisible();
    expect(screen.getByRole('button', { name: /Friday/ })).toHaveAttribute('aria-pressed', 'true');
    expect(onChanged).not.toHaveBeenCalled();
  });

  it('asks to log in again, and forgets the account, when the login has expired', async () => {
    signedIn();
    mocks.setFallbackSavedPlaylist.mockRejectedValue({ status: 401, message: 'Unauthorized' });
    const { user } = renderForm();

    await user.click(await screen.findByRole('button', { name: /Friday/ }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('You were signed out. Log in again to use your playlists.')).toBeVisible();
    await waitFor(() => expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('false'));
  });

  it('does not take a refused Spotify save for a lost login', async () => {
    mocks.setFallbackPlaylist.mockRejectedValue({ status: 401, message: 'The session is locked' });
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockResolvedValue({ id: 'u1', username: 'ann', role: 'User', createdAt: '' });
    const { user } = renderForm();

    await user.type(screen.getByLabelText('Playlist link'), 'spotify:playlist:abc');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('The session is locked')).toBeVisible();
    expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('true');
  });

  it('removes a saved playlist the same way as a Spotify one', async () => {
    const { user, onChanged } = renderForm({ fallbackPlaylistName: 'Friday', fallbackSavedPlaylistId: 'p1' });

    await user.click(screen.getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(mocks.setFallbackPlaylist).toHaveBeenCalledWith('s1', null, false));
    expect(onChanged).toHaveBeenCalled();
  });
});
