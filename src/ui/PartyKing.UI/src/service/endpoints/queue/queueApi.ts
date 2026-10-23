import { axios } from '@/service/request';
import { SongDto, SpotifyTrackDto } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

const withCredentials = { withCredentials: true };

export const queueApi = {
  getQueue: () => unwrap<SongDto[]>(axios.get('/queue/songs')),

  enqueue: (input: string) =>
    unwrap<SongDto>(axios.post('/queue/songs', { input })),

  /** Host only. Marks the current song as played and starts the next; rejects with status 404 when the queue is empty. */
  next: () =>
    unwrap<SongDto>(axios.post('/queue/songs/next', null, withCredentials)),

  searchSpotify: (query: string) =>
    unwrap<SpotifyTrackDto[]>(
      axios.get('/spotify/tracks', { params: { query } }),
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
