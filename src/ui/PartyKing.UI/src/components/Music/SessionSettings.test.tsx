import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SessionSettings } from './SessionSettings';

const mocks = vi.hoisted(() => ({
  rename: vi.fn(),
  changePassword: vi.fn(),
  getSlideshowSettings: vi.fn(),
  updateSlideshowSettings: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: {
    renameSession: mocks.rename,
    changePassword: mocks.changePassword,
    getSlideshowSettings: mocks.getSlideshowSettings,
    updateSlideshowSettings: mocks.updateSlideshowSettings,
  },
}));

const session = { id: 's1', code: 'ABC123', name: 'Party', hostName: 'Host', hasHost: true };

const openDialog = async (onChanged = vi.fn()) => {
  const user = userEvent.setup();
  render(<SessionSettings session={session} onChanged={onChanged} />);
  await user.click(screen.getByRole('button', { name: 'Session settings' }));
  return { user, onChanged };
};

describe('SessionSettings', () => {
  beforeEach(() => {
    mocks.rename.mockReset().mockResolvedValue(undefined);
    mocks.changePassword.mockReset().mockResolvedValue(undefined);
    mocks.updateSlideshowSettings.mockReset().mockResolvedValue(undefined);
    mocks.getSlideshowSettings
      .mockReset()
      .mockResolvedValue({ autoRepeat: true, startFromBeginning: false, slideTime: '00:00:10' });
  });

  it('keeps the settings closed until the gear is pressed', async () => {
    render(<SessionSettings session={session} onChanged={vi.fn()} />);

    expect(screen.queryByText('Session settings')).not.toBeInTheDocument();
    expect(mocks.getSlideshowSettings).not.toHaveBeenCalled();
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
});
