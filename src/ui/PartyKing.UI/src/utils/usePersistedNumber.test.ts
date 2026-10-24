import { act, renderHook } from '@testing-library/react';
import { usePersistedNumber } from './usePersistedNumber';

describe('usePersistedNumber', () => {
  beforeEach(() => localStorage.clear());

  it('uses the default when nothing is stored', () => {
    const { result } = renderHook(() => usePersistedNumber('key', 0.5, 0, 1));

    expect(result.current[0]).toBe(0.5);
  });

  it('reads a stored value', () => {
    localStorage.setItem('key', '0.8');

    const { result } = renderHook(() => usePersistedNumber('key', 0.5, 0, 1));

    expect(result.current[0]).toBe(0.8);
  });

  it('falls back to the default for a stored value that is not a number', () => {
    localStorage.setItem('key', 'loud');

    const { result } = renderHook(() => usePersistedNumber('key', 0.5, 0, 1));

    expect(result.current[0]).toBe(0.5);
  });

  it('clamps a stored value to the range', () => {
    localStorage.setItem('key', '7');

    const { result } = renderHook(() => usePersistedNumber('key', 0.5, 0, 1));

    expect(result.current[0]).toBe(1);
  });

  it('stores an updated value, clamped to the range', () => {
    const { result } = renderHook(() => usePersistedNumber('key', 0.5, 0, 1));

    act(() => result.current[1](-2));

    expect(result.current[0]).toBe(0);
    expect(localStorage.getItem('key')).toBe('0');
  });
});
