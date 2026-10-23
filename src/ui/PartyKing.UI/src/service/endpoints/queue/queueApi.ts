import { axios } from '@/service/request';
import { SongDto, SpotifyTrackDto } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

const withCredentials = { withCredentials: true };

export const queueApi = {
  getQueue: (sessionId: string) =>
    unwrap<SongDto[]>(axios.get(`/sessions/${sessionId}/songs`)),

  enqueue: (sessionId: string, input: string) =>
    unwrap<SongDto>(axios.post(`/sessions/${sessionId}/songs`, { input })),

  /**
   * Host only. Marks the current song as played and starts the next; rejects with status 404 when the queue is
   * empty and 403 when the logged-in account is not the host of the session. `currentSongId` is the song the player
   * thinks is playing: when an admin has skipped it meanwhile nothing advances and the song really playing is
   * returned.
   */
  next: (sessionId: string, currentSongId?: number) =>
    unwrap<SongDto>(
      axios.post(`/sessions/${sessionId}/songs/next`, null, { ...withCredentials, params: { currentSongId } }),
    ),

  searchSpotify: (sessionId: string, query: string) =>
    unwrap<SpotifyTrackDto[]>(
      axios.get(`/sessions/${sessionId}/tracks`, { params: { query } }),
    ),

  /** Host only. Access token for the Spotify Web Playback SDK. */
  getSpotifyToken: () =>
    unwrap<string>(axios.get('/spotify/token', withCredentials)),
};

/** True for strings that should be queued directly instead of searched for. */
export const looksLikeSongLink = (input: string) => {
  const value = input.trim().toLowerCase();
  return value.startsWith('http://') || value.startsWith('https://') || value.startsWith('spotify:');
};
