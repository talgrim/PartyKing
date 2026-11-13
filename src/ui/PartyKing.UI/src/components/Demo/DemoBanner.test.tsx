import { act, render, screen } from '@testing-library/react';
import { DemoBanner } from './DemoBanner';

const NOW = new Date('2026-11-13T12:00:00Z');
const at = (offsetMs: number) => new Date(NOW.getTime() + offsetMs).toISOString();
const MINUTE = 60 * 1000;

describe('DemoBanner', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  it('shows how long the demo lasts and that Spotify is not part of it', () => {
    render(<DemoBanner demo={{ expiresAt: at(180 * MINUTE), serverTime: at(0) }} onEnded={vi.fn()} />);

    expect(screen.getByText('Ends in 3:00:00')).toBeVisible();
    expect(screen.getByRole('status')).toHaveTextContent(/Spotify is not available here/);
  });

  it('warns that the party is about to be deleted in the last five minutes', () => {
    render(<DemoBanner demo={{ expiresAt: at(4 * MINUTE), serverTime: at(0) }} onEnded={vi.fn()} />);

    expect(screen.getByText('Ends in 4:00')).toBeVisible();
    expect(screen.getByRole('status')).toHaveTextContent(/ends soon/);
  });

  it('tells the page when the time is up', () => {
    const onEnded = vi.fn();
    render(<DemoBanner demo={{ expiresAt: at(2000), serverTime: at(0) }} onEnded={onEnded} />);
    expect(onEnded).not.toHaveBeenCalled();

    act(() => void vi.advanceTimersByTime(2000));

    expect(onEnded).toHaveBeenCalled();
    expect(screen.getByText('Ends in 0:00')).toBeVisible();
  });
});
