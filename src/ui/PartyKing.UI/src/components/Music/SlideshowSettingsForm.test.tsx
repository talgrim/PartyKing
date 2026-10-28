import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SlideshowSettingsForm } from './SlideshowSettingsForm';

const mocks = vi.hoisted(() => ({ get: vi.fn(), update: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: { getSlideshowSettings: mocks.get, updateSlideshowSettings: mocks.update },
}));

describe('SlideshowSettingsForm', () => {
  beforeEach(() => {
    mocks.get.mockReset().mockResolvedValue({ autoRepeat: false, startFromBeginning: true, slideTime: '00:00:20' });
    mocks.update.mockReset().mockResolvedValue(undefined);
  });

  it('shows the settings of the session', async () => {
    render(<SlideshowSettingsForm sessionId="s1" />);

    expect(await screen.findByLabelText('Seconds per photo')).toHaveValue(20);
    expect(screen.getByLabelText('Start over after the last photo')).not.toBeChecked();
    expect(screen.getByLabelText('Show photos that were already presented again')).toBeChecked();
    expect(mocks.get).toHaveBeenCalledWith('s1');
  });

  it('saves the changed settings', async () => {
    const user = userEvent.setup();
    render(<SlideshowSettingsForm sessionId="s1" />);

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

  it('changes the seconds with the mouse wheel, within the allowed range', async () => {
    render(<SlideshowSettingsForm sessionId="s1" />);

    const seconds = await screen.findByLabelText('Seconds per photo');
    fireEvent.wheel(seconds, { deltaY: -100 });
    expect(seconds).toHaveValue(21);

    fireEvent.wheel(seconds, { deltaY: 100 });
    fireEvent.wheel(seconds, { deltaY: 100 });
    expect(seconds).toHaveValue(19);

    fireEvent.change(seconds, { target: { value: '1' } });
    fireEvent.wheel(seconds, { deltaY: 100 });
    expect(seconds).toHaveValue(1);
  });

  it('shows why the settings could not be loaded', async () => {
    mocks.get.mockRejectedValue({ status: 403, message: 'Only the host or an admin can change the session settings' });

    render(<SlideshowSettingsForm sessionId="s1" />);

    expect(await screen.findByText('Only the host or an admin can change the session settings')).toBeVisible();
  });
});
