import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { JoinQrCode } from './JoinQrCode';

const mocks = vi.hoisted(() => ({ createJoinToken: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: { createJoinToken: mocks.createJoinToken },
}));
vi.mock('qrcode.react', () => ({
  QRCodeSVG: ({ value }: { value: string }) => <div data-testid="qr" data-value={value} />,
}));

const inAnHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString();

describe('JoinQrCode', () => {
  beforeEach(() => {
    mocks.createJoinToken.mockReset();
  });

  it('shows no QR code until asked', () => {
    render(<JoinQrCode sessionId="s1" />);

    expect(screen.queryByTestId('qr')).not.toBeInTheDocument();
    expect(mocks.createJoinToken).not.toHaveBeenCalled();
  });

  it('shows a QR code that links to the session with the token', async () => {
    mocks.createJoinToken.mockResolvedValue({ token: 'a+b/c', expiresAt: inAnHour() });
    render(<JoinQrCode sessionId="s1" />);

    await userEvent.click(screen.getByRole('button', { name: 'Show QR code' }));

    const qr = await screen.findByTestId('qr');
    expect(qr).toHaveAttribute('data-value', `${window.location.origin}/s/s1?join=a%2Bb%2Fc`);
    expect(mocks.createJoinToken).toHaveBeenCalledWith('s1');
  });

  it('replaces the QR code before its token expires', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      mocks.createJoinToken
        .mockResolvedValueOnce({ token: 'first', expiresAt: new Date(Date.now() + 60_000).toISOString() })
        .mockResolvedValue({ token: 'second', expiresAt: inAnHour() });
      render(<JoinQrCode sessionId="s1" />);

      await userEvent.click(screen.getByRole('button', { name: 'Show QR code' }));
      expect((await screen.findByTestId('qr')).getAttribute('data-value')).toContain('first');

      await act(() => vi.advanceTimersByTimeAsync(31_000));

      expect((await screen.findByTestId('qr')).getAttribute('data-value')).toContain('second');
    } finally {
      vi.useRealTimers();
    }
  });

  it('says so when the QR code cannot be created', async () => {
    mocks.createJoinToken.mockRejectedValue({ status: 403, message: 'Only the host or an admin can create a join code' });
    render(<JoinQrCode sessionId="s1" />);

    await userEvent.click(screen.getByRole('button', { name: 'Show QR code' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Only the host or an admin');
    expect(screen.queryByTestId('qr')).not.toBeInTheDocument();
  });

  it('stops asking for tokens once closed', async () => {
    mocks.createJoinToken.mockResolvedValue({ token: 't', expiresAt: inAnHour() });
    render(<JoinQrCode sessionId="s1" />);

    await userEvent.click(screen.getByRole('button', { name: 'Show QR code' }));
    await screen.findByTestId('qr');
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    expect(screen.queryByTestId('qr')).not.toBeInTheDocument();
  });
});
