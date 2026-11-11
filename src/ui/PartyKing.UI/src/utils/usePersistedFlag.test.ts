import { act, renderHook } from '@testing-library/react';
import { usePersistedFlag } from './usePersistedFlag';

describe('usePersistedFlag', () => {
  beforeEach(() => localStorage.clear());

  it('uses the default when nothing is stored, and reads a stored value', () => {
    expect(renderHook(() => usePersistedFlag('flag', true)).result.current[0]).toBe(true);

    localStorage.setItem('flag', 'false');

    expect(renderHook(() => usePersistedFlag('flag', true)).result.current[0]).toBe(false);
  });

  it('stores an updated value', () => {
    const { result } = renderHook(() => usePersistedFlag('flag', false));

    act(() => result.current[1](true));

    expect(result.current[0]).toBe(true);
    expect(localStorage.getItem('flag')).toBe('true');
  });

  it('shows a change to every component that uses the same key, and only to them', () => {
    const first = renderHook(() => usePersistedFlag('flag', false));
    const second = renderHook(() => usePersistedFlag('flag', false));
    const other = renderHook(() => usePersistedFlag('other', false));

    act(() => first.result.current[1](true));

    expect(second.result.current[0]).toBe(true);
    expect(other.result.current[0]).toBe(false);
  });

  it('keeps the value until the page is closed when storage is unavailable', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota');
    });
    const { result } = renderHook(() => usePersistedFlag('unsaved-flag', false));

    act(() => result.current[1](true));

    expect(result.current[0]).toBe(true);
    setItem.mockRestore();
  });
});
