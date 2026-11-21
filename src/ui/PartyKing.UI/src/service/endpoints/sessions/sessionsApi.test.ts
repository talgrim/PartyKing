import { axios } from '@/service/request';
import { sessionsApi } from './sessionsApi';

vi.mock('@/service/request', () => ({ axios: { put: vi.fn(), post: vi.fn(), delete: vi.fn() } }));

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

  it('deletes a session at once, with the cookies', async () => {
    vi.mocked(axios.delete).mockResolvedValue(undefined);

    await sessionsApi.deleteSession('session-1');

    expect(axios.delete).toHaveBeenCalledWith('/sessions/session-1', { withCredentials: true });
  });

  it('asks for the deletion of a session with a body-less POST, with the cookies', async () => {
    vi.mocked(axios.post).mockResolvedValue(undefined);

    await sessionsApi.requestDeletion('session-1');

    expect(axios.post).toHaveBeenCalledWith('/sessions/session-1/deletion-request', undefined, {
      withCredentials: true,
    });
  });
});
