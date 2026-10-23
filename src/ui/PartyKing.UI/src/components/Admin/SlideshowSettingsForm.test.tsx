import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SlideshowSettingsForm } from './SlideshowSettingsForm';

const mocks = vi.hoisted(() => ({ get: vi.fn(), update: vi.fn() }));

vi.mock('@/service/endpoints/admin/adminApi', () => ({
  adminApi: { getSlideshowSettings: mocks.get, updateSlideshowSettings: mocks.update },
}));

describe('SlideshowSettingsForm', () => {
  beforeEach(() => {
    mocks.get.mockReset().mockResolvedValue({ autoRepeat: false, startFromBeginning: true, slideTime: '00:00:20' });
    mocks.update.mockReset().mockResolvedValue(undefined);
  });

  it('shows the settings of the session', async () => {
    render(<SlideshowSettingsForm sessionId="s1" onLoggedOut={vi.fn()} />);

    expect(await screen.findByLabelText('Seconds per photo')).toHaveValue(20);
    expect(screen.getByLabelText('Start over after the last photo')).not.toBeChecked();
    expect(screen.getByLabelText('Show photos that were already presented again')).toBeChecked();
    expect(mocks.get).toHaveBeenCalledWith('s1');
  });

  it('saves the changed settings', async () => {
    const user = userEvent.setup();
    render(<SlideshowSettingsForm sessionId="s1" onLoggedOut={vi.fn()} />);

    const seconds = await screen.findByLabelText('Seconds per photo');
    await user.clear(seconds);
    await user.type(seconds, '90');
    await user.click(screen.getByLabelText('Start over after the last photo'));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() =>
      expect(mocks.update).toHaveBeenCalledWith('s1', {
        autoRepeat: true,
        startFromBeginning: true,
        slideTime: '00:01:30',
      }),
    );
    expect(await screen.findByText('Slideshow settings saved')).toBeInTheDocument();
  });

  it('goes back to the login when the admin session is gone', async () => {
    const onLoggedOut = vi.fn();
    mocks.get.mockRejectedValue({ status: 401 });

    render(<SlideshowSettingsForm sessionId="s1" onLoggedOut={onLoggedOut} />);

    await waitFor(() => expect(onLoggedOut).toHaveBeenCalled());
  });
});
