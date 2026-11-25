import type { MockInstance } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { SessionSettings } from './SessionSettings';

const mocks = vi.hoisted(() => ({
  rename: vi.fn(),
  changePassword: vi.fn(),
  getSlideshowSettings: vi.fn(),
  updateSlideshowSettings: vi.fn(),
  getHostPlaylists: vi.fn(),
  setFallbackPlaylist: vi.fn(),
  getPhotoCount: vi.fn(),
  getDemoConfig: vi.fn(),
  deleteSession: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: {
    renameSession: mocks.rename,
    changePassword: mocks.changePassword,
    getSlideshowSettings: mocks.getSlideshowSettings,
    updateSlideshowSettings: mocks.updateSlideshowSettings,
    getHostPlaylists: mocks.getHostPlaylists,
    setFallbackPlaylist: mocks.setFallbackPlaylist,
    getPhotoCount: mocks.getPhotoCount,
    deleteSession: mocks.deleteSession,
    photoArchiveUrl: (id: string) => `/api/sessions/${id}/photos/archive`,
  },
}));

vi.mock('@/service/endpoints/demo/demoApi', () => ({ demoApi: { getConfig: mocks.getDemoConfig } }));

const session = { id: 's1', code: 'ABC123', name: 'Party', hostName: 'Host', hasHost: true };

const openDialog = async (onChanged = vi.fn(), onDeleted = vi.fn()) => {
  const user = userEvent.setup();
  render(<SessionSettings session={session} onChanged={onChanged} onDeleted={onDeleted} />);
  await user.click(screen.getByRole('button', { name: 'Session settings' }));
  return { user, onChanged, onDeleted };
};

describe('SessionSettings', () => {
  beforeEach(() => {
    mocks.getDemoConfig.mockReset().mockResolvedValue({ enabled: false, serverTime: '2026-11-13T12:00:00Z' });
    mocks.rename.mockReset().mockResolvedValue(undefined);
    mocks.changePassword.mockReset().mockResolvedValue(undefined);
    mocks.updateSlideshowSettings.mockReset().mockResolvedValue(undefined);
    mocks.getHostPlaylists.mockReset().mockResolvedValue([]);
    mocks.setFallbackPlaylist.mockReset().mockResolvedValue(undefined);
    mocks.getPhotoCount.mockReset().mockResolvedValue({ count: 2 });
    mocks.deleteSession.mockReset().mockResolvedValue(undefined);
    mocks.getSlideshowSettings
      .mockReset()
      .mockResolvedValue({ autoRepeat: true, startFromBeginning: false, slideTime: '00:00:10' });
  });

  it('keeps the settings closed until the gear is pressed', async () => {
    render(<SessionSettings session={session} onChanged={vi.fn()} onDeleted={vi.fn()} />);

    expect(screen.queryByText('Session settings')).not.toBeInTheDocument();
    expect(mocks.getSlideshowSettings).not.toHaveBeenCalled();
  });

  it('offers the photo archive in the settings', async () => {
    await openDialog();

    expect(await screen.findByRole('link', { name: 'Download as ZIP' })).toHaveAttribute(
      'href',
      '/api/sessions/s1/photos/archive',
    );
  });

  it('closes with the X in the corner', async () => {
    const { user } = await openDialog();

    await user.click(screen.getByRole('button', { name: 'Close' }));

    await waitFor(() => expect(screen.queryByLabelText('Session name')).not.toBeInTheDocument());
  });

  it('renames the session and tells the page', async () => {
    const { user, onChanged } = await openDialog();

    const input = screen.getByLabelText('Session name');
    expect(input).toHaveValue('Party');
    expect(screen.getByRole('button', { name: 'Save name' })).toBeDisabled();

    await user.clear(input);
    await user.type(input, '  New name ');
    await user.click(screen.getByRole('button', { name: 'Save name' }));

    await waitFor(() => expect(mocks.rename).toHaveBeenCalledWith('s1', 'New name'));
    expect(await screen.findByText('Name saved')).toBeVisible();
    expect(onChanged).toHaveBeenCalled();
  });

  it('shows the reason when the rename is refused', async () => {
    mocks.rename.mockRejectedValue({ status: 403, message: 'Only the host or an admin can change the session settings' });
    const { user, onChanged } = await openDialog();

    const input = screen.getByLabelText('Session name');
    await user.type(input, '!');
    await user.click(screen.getByRole('button', { name: 'Save name' }));

    expect(await screen.findByText('Only the host or an admin can change the session settings')).toBeVisible();
    expect(onChanged).not.toHaveBeenCalled();
  });

  it('only allows a password of at least 4 characters and clears it after saving', async () => {
    const { user } = await openDialog();

    const input = screen.getByLabelText('New password');
    const button = screen.getByRole('button', { name: 'Change password' });
    await user.type(input, 'abc');
    expect(button).toBeDisabled();

    await user.type(input, 'd');
    await user.click(button);

    await waitFor(() => expect(mocks.changePassword).toHaveBeenCalledWith('s1', 'abcd'));
    expect(await screen.findByText(/Password changed/)).toBeVisible();
    expect(input).toHaveValue('');
  });

  it('shows the slideshow settings of the session', async () => {
    await openDialog();

    expect(await screen.findByLabelText('Seconds per photo')).toHaveValue(10);
    expect(mocks.getSlideshowSettings).toHaveBeenCalledWith('s1');
  });

  it('has the fallback playlist, which is a Spotify one, everywhere but in the demo', async () => {
    await openDialog();

    expect(await screen.findByRole('region', { name: 'Fallback playlist' })).toBeVisible();
  });

  it('leaves the fallback playlist out of the demo', async () => {
    mocks.getDemoConfig.mockResolvedValue({ enabled: true, serverTime: '2026-11-13T12:00:00Z' });
    const user = userEvent.setup();
    render(
      <SWRConfig value={{ provider: () => new Map() }}>
        <SessionSettings session={session} onChanged={vi.fn()} onDeleted={vi.fn()} />
      </SWRConfig>,
    );
    await waitFor(() => expect(mocks.getDemoConfig).toHaveBeenCalled());
    await user.click(screen.getByRole('button', { name: 'Session settings' }));

    await waitFor(() => expect(screen.queryByRole('region', { name: 'Fallback playlist' })).not.toBeInTheDocument());
    expect(screen.getByRole('region', { name: 'Slideshow' })).toBeVisible();
  });

  describe('deleting the session', () => {
    let confirm: MockInstance<typeof window.confirm>;

    beforeEach(() => {
      confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    });

    afterEach(() => confirm.mockRestore());

    it('has a section that deletes the session now, and tells the page once it is gone', async () => {
      const { user, onDeleted } = await openDialog();
      const section = screen.getByRole('region', { name: 'Delete session' });

      await user.click(within(section).getByRole('button', { name: 'Delete session' }));

      await waitFor(() => expect(onDeleted).toHaveBeenCalledTimes(1));
      expect(mocks.deleteSession).toHaveBeenCalledWith('s1');
    });

    it('deletes nothing when the confirmation is refused', async () => {
      confirm.mockReturnValue(false);
      const { user, onDeleted } = await openDialog();

      await user.click(within(screen.getByRole('region', { name: 'Delete session' })).getByRole('button', { name: 'Delete session' }));

      expect(mocks.deleteSession).not.toHaveBeenCalled();
      expect(onDeleted).not.toHaveBeenCalled();
    });

    it('is left out of the demo, whose sessions end on their own', async () => {
      mocks.getDemoConfig.mockResolvedValue({ enabled: true, serverTime: '2026-11-13T12:00:00Z' });
      const user = userEvent.setup();
      render(
        <SWRConfig value={{ provider: () => new Map() }}>
          <SessionSettings session={session} onChanged={vi.fn()} onDeleted={vi.fn()} />
        </SWRConfig>,
      );
      await waitFor(() => expect(mocks.getDemoConfig).toHaveBeenCalled());
      await user.click(screen.getByRole('button', { name: 'Session settings' }));

      await waitFor(() => expect(screen.queryByRole('region', { name: 'Delete session' })).not.toBeInTheDocument());
    });
  });
});
