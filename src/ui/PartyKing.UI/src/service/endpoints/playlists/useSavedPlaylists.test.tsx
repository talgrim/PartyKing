import { renderHook, waitFor } from '@testing-library/react';
import { ReactNode } from 'react';
import { SWRConfig } from 'swr';
import { useSavedPlaylists } from './useSavedPlaylists';

const mocks = vi.hoisted(() => ({ list: vi.fn() }));

vi.mock('./playlistsApi', () => ({ playlistsApi: { list: mocks.list } }));

const wrapper = ({ children }: { children: ReactNode }) => (
  <SWRConfig value={{ provider: () => new Map() }}>{children}</SWRConfig>
);

const friday = { id: 'p1', name: 'Friday', createdAt: '2026-11-09T12:00:00Z', songCount: 3 };

describe('useSavedPlaylists', () => {
  beforeEach(() => {
    mocks.list.mockReset();
  });

  it('asks nothing while it is not enabled', () => {
    const { result } = renderHook(() => useSavedPlaylists(false), { wrapper });

    expect(result.current.playlists).toBeUndefined();
    expect(mocks.list).not.toHaveBeenCalled();
  });

  it('answers the playlists of the account once enabled', async () => {
    mocks.list.mockResolvedValue([friday]);

    const { result } = renderHook(() => useSavedPlaylists(true), { wrapper });

    await waitFor(() => expect(result.current.playlists).toEqual([friday]));
    expect(result.current.error).toBeUndefined();
  });

  it('hands on the failure, so a 401 can be told from the rest', async () => {
    mocks.list.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    const { result } = renderHook(() => useSavedPlaylists(true), { wrapper });

    await waitFor(() => expect(result.current.error?.status).toBe(401));
    expect(result.current.playlists).toBeUndefined();
  });

  it('asks again when it is enabled again, because the account may have saved a playlist meanwhile', async () => {
    mocks.list.mockResolvedValue([friday]);
    const { result, rerender } = renderHook(({ enabled }) => useSavedPlaylists(enabled), {
      wrapper,
      initialProps: { enabled: true },
    });
    await waitFor(() => expect(result.current.playlists).toEqual([friday]));

    rerender({ enabled: false });
    rerender({ enabled: true });

    await waitFor(() => expect(mocks.list).toHaveBeenCalledTimes(2));
  });
});
