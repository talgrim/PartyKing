import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { SaveQueueAsPlaylist } from './SaveQueueAsPlaylist';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';
import { PlaylistSection } from '@/service/endpoints/playlists/types';

const mocks = vi.hoisted(() => ({ saveQueue: vi.fn(), getPreview: vi.fn(), me: vi.fn() }));

vi.mock('@/service/endpoints/playlists/playlistsApi', () => ({
  playlistsApi: { saveQueue: mocks.saveQueue, getPreview: mocks.getPreview },
}));
vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));

const preview = (counts: [PlaylistSection, number][]) => ({
  sections: counts.map(([section, count]) => ({ section, count })),
});

const defaultPreview = preview([
  [PlaylistSection.History, 5],
  [PlaylistSection.Current, 1],
  [PlaylistSection.Queue, 3],
  [PlaylistSection.Fallback, 2],
]);

const openDialog = async () => {
  const user = userEvent.setup();
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <SaveQueueAsPlaylist sessionId="s1" sessionName="Friday party" />
    </SWRConfig>,
  );
  await user.click(screen.getByRole('button', { name: 'Save as playlist' }));
  return user;
};

const section = (name: string) => screen.getByRole('checkbox', { name: new RegExp(name) });

describe('SaveQueueAsPlaylist', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.saveQueue.mockReset().mockResolvedValue({ id: 'p1', name: 'Friday party', createdAt: '', songCount: 4 });
    mocks.getPreview.mockReset().mockResolvedValue(defaultPreview);
    mocks.me.mockReset();
  });

  it('keeps the dialog closed, and asks for no counts, until the button is pressed', () => {
    render(<SaveQueueAsPlaylist sessionId="s1" sessionName="Party" />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(mocks.getPreview).not.toHaveBeenCalled();
  });

  it('starts with the session name and the playing song and up next ticked', async () => {
    await openDialog();

    expect(await screen.findByRole('textbox', { name: 'Playlist name' })).toHaveValue('Friday party');
    expect(section('Played earlier')).not.toBeChecked();
    expect(section('Playing now')).toBeChecked();
    expect(section('Up next')).toBeChecked();
    expect(section('From the fallback playlist')).not.toBeChecked();
  });

  it('shows how many songs each section holds', async () => {
    await openDialog();

    await waitFor(() => expect(screen.getByLabelText('5 songs')).toBeInTheDocument());
    expect(screen.getByLabelText('1 song')).toBeInTheDocument();
    expect(screen.getByLabelText('3 songs')).toBeInTheDocument();
    expect(screen.getByLabelText('2 songs')).toBeInTheDocument();
  });

  it('saves the ticked sections under the name and reports how many songs were saved', async () => {
    const user = await openDialog();
    await screen.findByLabelText('5 songs');

    await user.click(section('Played earlier'));
    await user.click(section('Up next'));
    await user.clear(screen.getByRole('textbox', { name: 'Playlist name' }));
    await user.type(screen.getByRole('textbox', { name: 'Playlist name' }), '  Mine ');
    await user.click(screen.getByRole('button', { name: 'Save playlist' }));

    expect(mocks.saveQueue).toHaveBeenCalledWith('s1', 'Mine', [PlaylistSection.History, PlaylistSection.Current]);
    expect(await screen.findByRole('status')).toHaveTextContent('Saved 4 songs as “Friday party”.');
    expect(screen.queryByRole('button', { name: 'Save playlist' })).not.toBeInTheDocument();
  });

  it('disables a section with no songs and leaves it out of the request', async () => {
    mocks.getPreview.mockResolvedValue(
      preview([
        [PlaylistSection.History, 0],
        [PlaylistSection.Current, 1],
        [PlaylistSection.Queue, 0],
        [PlaylistSection.Fallback, 0],
      ]),
    );
    const user = await openDialog();
    await waitFor(() => expect(section('Up next')).toBeDisabled());

    expect(section('Up next')).not.toBeChecked();
    await user.click(screen.getByRole('button', { name: 'Save playlist' }));

    expect(mocks.saveQueue).toHaveBeenCalledWith('s1', 'Friday party', [PlaylistSection.Current]);
  });

  it('cannot save while nothing is ticked', async () => {
    const user = await openDialog();
    await screen.findByLabelText('5 songs');

    await user.click(section('Playing now'));
    await user.click(section('Up next'));

    expect(screen.getByRole('button', { name: 'Save playlist' })).toBeDisabled();
  });

  it('cannot save without a name', async () => {
    const user = await openDialog();
    await screen.findByLabelText('5 songs');

    await user.clear(screen.getByRole('textbox', { name: 'Playlist name' }));

    expect(screen.getByRole('button', { name: 'Save playlist' })).toBeDisabled();
  });

  it('limits the name to 60 characters', async () => {
    await openDialog();

    expect(await screen.findByRole('textbox', { name: 'Playlist name' })).toHaveAttribute('maxlength', '60');
  });

  it('shows the message of the server when saving fails, and keeps the form', async () => {
    mocks.saveQueue.mockRejectedValue({ status: 400, message: 'You can save at most 50 playlists' });
    const user = await openDialog();
    await screen.findByLabelText('5 songs');

    await user.click(screen.getByRole('button', { name: 'Save playlist' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('You can save at most 50 playlists');
    expect(screen.getByRole('button', { name: 'Save playlist' })).toBeEnabled();
  });

  it('asks to log in again, and forgets the account, when the login has expired', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockResolvedValue({ id: 'u1', username: 'ann', role: 'User', createdAt: '' });
    mocks.saveQueue.mockRejectedValue({ status: 401, message: 'Unauthorized' });
    const user = await openDialog();
    await screen.findByLabelText('5 songs');

    await user.click(screen.getByRole('button', { name: 'Save playlist' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('You were signed out. Log in again to save the playlist.');
    await waitFor(() => expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('false'));
  });

  it('says so when the counts cannot be loaded', async () => {
    mocks.getPreview.mockRejectedValue({ status: 500, message: '' });
    await openDialog();

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not count the songs');
  });

  it('starts over with the defaults and fresh counts when it is opened again', async () => {
    const user = await openDialog();
    await screen.findByLabelText('5 songs');
    await user.click(section('Played earlier'));
    await user.click(screen.getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Save as playlist' }));

    expect(await screen.findByRole('checkbox', { name: /Played earlier/ })).not.toBeChecked();
    // SWR revalidates cached data a frame after it shows it
    await waitFor(() => expect(mocks.getPreview).toHaveBeenCalledTimes(2));
  });
});
