import { axios } from '@/service/request';
import { lyricsApi } from './lyricsApi';

vi.mock('@/service/request', () => ({ axios: { get: vi.fn(), put: vi.fn() } }));

describe('lyricsApi', () => {
  it('saves the offset of the song being played', async () => {
    vi.mocked(axios.put).mockResolvedValue(undefined);

    await lyricsApi.setOffset('session-1', -1500);

    expect(axios.put).toHaveBeenCalledWith('/sessions/session-1/now-playing/lyrics/offset', { offsetMs: -1500 });
  });
});
