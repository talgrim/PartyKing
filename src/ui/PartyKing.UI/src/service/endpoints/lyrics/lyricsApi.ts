import { axios } from '@/service/request';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { LyricsDto } from './types';

// The axios response interceptor unwraps `response.data`, so this resolves to the payload itself.
export const lyricsApi = {
  /** Host or admin. The lyrics of the song being played, or null when it has none (or nothing is playing). */
  getLyrics: async (sessionId: string): Promise<LyricsDto | null> => {
    try {
      return (await axios.get(`/sessions/${sessionId}/now-playing/lyrics`)) as LyricsDto;
    } catch (e) {
      if ((e as { status?: number })?.status === HTTP_STATUS_CODE.NOT_FOUND) return null;
      throw e;
    }
  },
};
