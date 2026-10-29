import { axios } from '@/service/request';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { LyricsDto } from './types';

// The axios response interceptor unwraps `response.data`, so this resolves to the payload itself.
export const lyricsApi = {
  /** Anyone who unlocked the session. The lyrics of the song being played, or null when it has none (or nothing is playing). */
  getLyrics: async (sessionId: string): Promise<LyricsDto | null> => {
    try {
      return (await axios.get(`/sessions/${sessionId}/now-playing/lyrics`)) as LyricsDto;
    } catch (e) {
      if ((e as { status?: number })?.status === HTTP_STATUS_CODE.NOT_FOUND) return null;
      throw e;
    }
  },

  /** Host or admin. Saves the timing offset of the song being played; the server pushes it to the session. */
  setOffset: async (sessionId: string, offsetMs: number): Promise<void> => {
    await axios.put(`/sessions/${sessionId}/now-playing/lyrics/offset`, { offsetMs });
  },
};
