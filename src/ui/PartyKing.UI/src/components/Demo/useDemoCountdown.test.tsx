import { act, renderHook } from '@testing-library/react';
import { useDemoCountdown } from './useDemoCountdown';

const NOW = new Date('2026-11-13T12:00:00Z');
const at = (offsetMs: number) => new Date(NOW.getTime() + offsetMs).toISOString();

describe('useDemoCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  it('knows nothing until the end is known', () => {
    const { result } = renderHook(() => useDemoCountdown(undefined));

    expect(result.current).toBeNull();
  });

  it('counts down by the second and ends when the time is up', () => {
    const demo = { expiresAt: at(3000), serverTime: at(0) };
    const { result } = renderHook(() => useDemoCountdown(demo));

    expect(result.current).toMatchObject({ remainingMs: 3000, ended: false });

    act(() => void vi.advanceTimersByTime(2000));
    expect(result.current).toMatchObject({ remainingMs: 1000, ended: false, level: 'last' });

    act(() => void vi.advanceTimersByTime(1000));
    expect(result.current).toMatchObject({ remainingMs: 0, ended: true });
  });

  it('is not moved by a device clock that disagrees with the server', () => {
    // The device is an hour behind the server: the session still ends ten minutes from now
    const demo = { expiresAt: at(3_600_000 + 600_000), serverTime: at(3_600_000) };
    const { result } = renderHook(() => useDemoCountdown(demo));

    expect(result.current).toMatchObject({ remainingMs: 600_000, level: 'normal' });
  });

  it('warns in the last five minutes', () => {
    const demo = { expiresAt: at(5 * 60 * 1000 + 1000), serverTime: at(0) };
    const { result } = renderHook(() => useDemoCountdown(demo));
    expect(result.current?.level).toBe('normal');

    act(() => void vi.advanceTimersByTime(1000));
    expect(result.current?.level).toBe('soon');
  });
});
