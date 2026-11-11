import { axios } from '@/service/request';
import { playlistsApi } from './playlistsApi';
import { PlaylistSection } from './types';

vi.mock('@/service/request', () => ({ axios: { get: vi.fn(), post: vi.fn() } }));

describe('playlistsApi', () => {
  it('saves the chosen sections under the name, with the account cookie', async () => {
    vi.mocked(axios.post).mockResolvedValue({ id: 'p1', name: 'Friday', createdAt: '', songCount: 3 });

    const saved = await playlistsApi.saveQueue('session-1', 'Friday', [PlaylistSection.Current, PlaylistSection.Queue]);

    expect(axios.post).toHaveBeenCalledWith(
      '/sessions/session-1/saved-playlists',
      { name: 'Friday', sections: [1, 2] },
      { withCredentials: true },
    );
    expect(saved.songCount).toBe(3);
  });

  it('lists the playlists of the account, with the account cookie', async () => {
    const playlists = [{ id: 'p1', name: 'Friday', createdAt: '', songCount: 3 }];
    vi.mocked(axios.get).mockResolvedValue(playlists);

    const listed = await playlistsApi.list();

    expect(axios.get).toHaveBeenCalledWith('/account/saved-playlists', { withCredentials: true });
    expect(listed).toEqual(playlists);
  });

  it('asks how many songs each section holds, with the account cookie', async () => {
    vi.mocked(axios.get).mockResolvedValue({ sections: [{ section: 0, count: 4 }] });

    const preview = await playlistsApi.getPreview('session-1');

    expect(axios.get).toHaveBeenCalledWith('/sessions/session-1/saved-playlists/preview', { withCredentials: true });
    expect(preview.sections).toEqual([{ section: 0, count: 4 }]);
  });
});
