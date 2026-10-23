import { renderHook, waitFor } from '@testing-library/react';
import { ReactNode } from 'react';
import { SWRConfig } from 'swr';
import { ADMIN_HINT_KEY, useIsAdmin } from './useIsAdmin';

const mocks = vi.hoisted(() => ({ me: vi.fn() }));

vi.mock('./adminApi', () => ({ adminApi: { me: mocks.me } }));

const wrapper = ({ children }: { children: ReactNode }) => (
  <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>{children}</SWRConfig>
);

describe('useIsAdmin', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.me.mockReset();
  });

  it('asks nothing and answers false without an admin hint', () => {
    const { result } = renderHook(() => useIsAdmin(), { wrapper });

    expect(result.current).toBe(false);
    expect(mocks.me).not.toHaveBeenCalled();
  });

  it('answers true once the admin cookie is verified', async () => {
    localStorage.setItem(ADMIN_HINT_KEY, 'true');
    mocks.me.mockResolvedValue(undefined);

    const { result } = renderHook(() => useIsAdmin(), { wrapper });

    expect(result.current).toBe(false);
    await waitFor(() => expect(result.current).toBe(true));
  });

  it('drops the hint when the cookie is no longer valid', async () => {
    localStorage.setItem(ADMIN_HINT_KEY, 'true');
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    const { result } = renderHook(() => useIsAdmin(), { wrapper });

    await waitFor(() => expect(localStorage.getItem(ADMIN_HINT_KEY)).toBe('false'));
    expect(result.current).toBe(false);
  });

  it('keeps the hint when the API cannot be reached', async () => {
    localStorage.setItem(ADMIN_HINT_KEY, 'true');
    mocks.me.mockRejectedValue({ status: 500, message: 'Boom' });

    const { result } = renderHook(() => useIsAdmin(), { wrapper });

    await waitFor(() => expect(mocks.me).toHaveBeenCalled());
    expect(result.current).toBe(false);
    expect(localStorage.getItem(ADMIN_HINT_KEY)).toBe('true');
  });
});
