import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { SavedPlaylistPicker } from './SavedPlaylistPicker';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';

const mocks = vi.hoisted(() => ({ list: vi.fn(), me: vi.fn() }));

vi.mock('@/service/endpoints/playlists/playlistsApi', () => ({ playlistsApi: { list: mocks.list } }));
vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));

const friday = { id: 'p1', name: 'Friday', createdAt: '2026-11-09T12:00:00Z', songCount: 3 };
const warmUp = { id: 'p2', name: 'Warm up', createdAt: '2026-11-08T12:00:00Z', songCount: 1 };

const renderPicker = ({ inUseId = null, selectedId = null }: { inUseId?: string | null; selectedId?: string | null } = {}) => {
  const onSelect = vi.fn();
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <SavedPlaylistPicker inUseId={inUseId} selectedId={selectedId} onSelect={onSelect} />
    </SWRConfig>,
  );
  return { user: userEvent.setup(), onSelect };
};

const signedIn = () => {
  localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
  mocks.me.mockResolvedValue({ id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' });
};

describe('SavedPlaylistPicker', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.list.mockReset().mockResolvedValue([friday, warmUp]);
    mocks.me.mockReset();
  });

  it('asks a visitor to log in, and asks for no playlists', async () => {
    const { user } = renderPicker();

    expect(screen.getByText('Log in to use the playlists you saved.')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Log in' }));

    expect(await screen.findByRole('dialog')).toBeVisible();
    expect(mocks.list).not.toHaveBeenCalled();
  });

  it('lists the playlists of the account with their song counts', async () => {
    signedIn();
    renderPicker();

    const list = await screen.findByRole('list', { name: 'Saved playlists' });

    expect(list).toHaveTextContent('Friday');
    expect(list).toHaveTextContent('3 songs');
    expect(list).toHaveTextContent('1 song');
    expect(screen.getByRole('button', { name: /Friday/ })).toHaveAttribute('aria-pressed', 'false');
  });

  it('marks the playlist that is selected and the one that is in use', async () => {
    signedIn();
    renderPicker({ inUseId: 'p2', selectedId: 'p1' });

    expect(await screen.findByRole('button', { name: /Warm up.*in use/ })).toBeVisible();
    expect(screen.getByRole('button', { name: /Friday/ })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /Friday/ })).not.toHaveTextContent('in use');
  });

  it('hands on the playlist that is picked', async () => {
    signedIn();
    const { user, onSelect } = renderPicker();

    await user.click(await screen.findByRole('button', { name: /Warm up/ }));

    expect(onSelect).toHaveBeenCalledWith('p2');
  });

  it('says so when the account has saved no playlist', async () => {
    signedIn();
    mocks.list.mockResolvedValue([]);
    renderPicker();

    expect(await screen.findByText(/You have not saved any playlist yet/)).toBeVisible();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('treats a 401 on the list as a visitor', async () => {
    signedIn();
    mocks.list.mockRejectedValue({ status: 401, message: 'Unauthorized' });
    renderPicker();

    await waitFor(() => expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('false'));
    expect(await screen.findByText('Log in to use the playlists you saved.')).toBeVisible();
  });

  it('says so when the playlists cannot be loaded', async () => {
    signedIn();
    mocks.list.mockRejectedValue({ status: 500, message: '' });
    renderPicker();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load your saved playlists');
  });
});
