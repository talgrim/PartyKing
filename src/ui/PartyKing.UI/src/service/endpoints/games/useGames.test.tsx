import { renderHook, waitFor, act } from '@testing-library/react';
import { SWRConfig } from 'swr';
import type { ReactNode } from 'react';
import { setLanguage } from '@/i18n';
import { gamesApi } from './gamesApi';
import { useGames } from './useGames';

vi.mock('./gamesApi', () => ({ gamesApi: { getGames: vi.fn() } }));

const wrapper = ({ children }: { children: ReactNode }) => (
  <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>{children}</SWRConfig>
);

describe('useGames', () => {
  beforeEach(() => {
    vi.mocked(gamesApi.getGames).mockResolvedValue([{ id: 'ringo', name: 'Ringo', description: 'd' }]);
  });

  it('fetches the games again when the language changes, so the names follow it', async () => {
    const { result } = renderHook(() => useGames('session-1'), { wrapper });
    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(gamesApi.getGames).toHaveBeenCalledTimes(1);

    await act(() => setLanguage('pl'));

    await waitFor(() => expect(gamesApi.getGames).toHaveBeenCalledTimes(2));
  });
});
