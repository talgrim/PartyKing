import { act, renderHook, waitFor } from '@testing-library/react';
import { ReactNode } from 'react';
import { SWRConfig } from 'swr';
import { ACCOUNT_HINT_KEY, useAccount, useIsAdmin } from './useAccount';

const mocks = vi.hoisted(() => ({ me: vi.fn() }));

vi.mock('./accountApi', () => ({ accountApi: { me: mocks.me } }));

const wrapper = ({ children }: { children: ReactNode }) => (
  <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>{children}</SWRConfig>
);

const admin = { id: 'a1', username: 'boss', role: 'Admin', createdAt: '2026-11-09T12:00:00Z' };
const user = { id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' };

describe('useAccount', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.me.mockReset();
  });

  it('asks nothing and answers nobody without an account hint', () => {
    const { result } = renderHook(() => useAccount(), { wrapper });

    expect(result.current.account).toBeNull();
    expect(result.current.isLoading).toBe(false);
    expect(mocks.me).not.toHaveBeenCalled();
  });

  it('answers who is signed in once the cookie is verified', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockResolvedValue(user);

    const { result } = renderHook(() => useAccount(), { wrapper });

    expect(result.current.account).toBeNull();
    await waitFor(() => expect(result.current.account).toEqual(user));
  });

  it('drops the hint, and reports no error, when the cookie is no longer valid', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    const { result } = renderHook(() => useAccount(), { wrapper });

    await waitFor(() => expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('false'));
    expect(result.current.account).toBeNull();
    expect(result.current.error).toBeUndefined();
  });

  it('keeps the hint, and reports the error, when the API cannot be reached', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockRejectedValue({ status: 500, message: 'Boom' });

    const { result } = renderHook(() => useAccount(), { wrapper });

    await waitFor(() => expect(result.current.error).toBeDefined());
    expect(result.current.account).toBeNull();
    expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('true');
  });

  it('asks even without a hint when told to always check, and sets the hint when someone is signed in', async () => {
    mocks.me.mockResolvedValue(admin);

    const { result } = renderHook(() => useAccount({ alwaysCheck: true }), { wrapper });

    await waitFor(() => expect(result.current.account).toEqual(admin));
    expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('true');
  });

  it('answers nobody, with no error, when told to always check and nobody is signed in', async () => {
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    const { result } = renderHook(() => useAccount({ alwaysCheck: true }), { wrapper });

    await waitFor(() => expect(mocks.me).toHaveBeenCalled());
    expect(result.current.account).toBeNull();
    expect(result.current.error).toBeUndefined();
    expect(result.current.isLoading).toBe(false);
  });

  it('starts asking after signedIn, and forgets the account after signedOut', async () => {
    mocks.me.mockResolvedValue(user);
    const { result } = renderHook(() => useAccount(), { wrapper });

    act(() => result.current.signedIn());
    await waitFor(() => expect(result.current.account).toEqual(user));
    expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('true');

    act(() => result.current.signedOut());
    await waitFor(() => expect(result.current.account).toBeNull());
    expect(localStorage.getItem(ACCOUNT_HINT_KEY)).toBe('false');
  });
});

describe('useIsAdmin', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.me.mockReset();
  });

  it('is true only for an admin account', async () => {
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    mocks.me.mockResolvedValue(admin);
    const asAdmin = renderHook(() => useIsAdmin(), { wrapper });
    await waitFor(() => expect(asAdmin.result.current).toBe(true));

    mocks.me.mockResolvedValue(user);
    const asUser = renderHook(() => useIsAdmin(), { wrapper });
    await waitFor(() => expect(mocks.me).toHaveBeenCalledTimes(2));

    expect(asUser.result.current).toBe(false);
  });
});
