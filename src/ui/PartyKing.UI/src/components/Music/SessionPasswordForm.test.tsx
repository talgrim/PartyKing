import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SessionPasswordForm } from './SessionPasswordForm';

const mocks = vi.hoisted(() => ({ join: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { join: mocks.join } }));

const submit = async (password: string) => {
  await userEvent.type(screen.getByLabelText('Session password'), password);
  await userEvent.click(screen.getByRole('button', { name: 'Join' }));
};

describe('SessionPasswordForm', () => {
  beforeEach(() => {
    mocks.join.mockReset();
  });

  it('joins with the typed password and reports success', async () => {
    mocks.join.mockResolvedValue(undefined);
    const onJoined = vi.fn();
    render(<SessionPasswordForm sessionId="s1" onJoined={onJoined} />);

    await submit('secret');

    expect(mocks.join).toHaveBeenCalledWith('s1', 'secret');
    expect(onJoined).toHaveBeenCalled();
  });

  it('says so when the password is wrong', async () => {
    mocks.join.mockRejectedValue({ status: 401 });
    const onJoined = vi.fn();
    render(<SessionPasswordForm sessionId="s1" onJoined={onJoined} />);

    await submit('nope');

    expect(await screen.findByText('Wrong password')).toBeVisible();
    expect(onJoined).not.toHaveBeenCalled();
  });

  it('says so when the rate limit is hit', async () => {
    mocks.join.mockRejectedValue({ status: 429 });
    render(<SessionPasswordForm sessionId="s1" onJoined={vi.fn()} />);

    await submit('nope');

    expect(await screen.findByText(/Too many attempts/)).toBeVisible();
  });

  it('does not submit an empty password', () => {
    render(<SessionPasswordForm sessionId="s1" onJoined={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Join' })).toBeDisabled();
  });
});
