import { axios } from '@/service/request';
import { sessionsApi } from './sessionsApi';

vi.mock('@/service/request', () => ({ axios: { put: vi.fn() } }));

describe('sessionsApi', () => {
  it('sets a saved playlist as the fallback, with the cookies', async () => {
    vi.mocked(axios.put).mockResolvedValue(undefined);

    await sessionsApi.setFallbackSavedPlaylist('session-1', 'playlist-1', true);

    expect(axios.put).toHaveBeenCalledWith(
      '/sessions/session-1/fallback-saved-playlist',
      { playlistId: 'playlist-1', shuffle: true },
      { withCredentials: true },
    );
  });
});
