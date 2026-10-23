import { axios } from '@/service/request';
import { PlayerCommand } from '../player/types';
import { SongDto } from '../queue/types';
import { SessionDto, SlideshowSettingsDto } from '../sessions/types';

const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

const withCredentials = { withCredentials: true };

export const adminApi = {
  login: (username: string, password: string) =>
    unwrap<void>(axios.post('/admin/login', { username, password }, withCredentials)),

  logout: () => unwrap<void>(axios.post('/admin/logout', null, withCredentials)),

  /** Resolves when the admin cookie is valid; rejects with status 401 otherwise. */
  me: () => unwrap<void>(axios.get('/admin/me', withCredentials)),

  createSession: (name: string | undefined, password: string) =>
    unwrap<SessionDto>(axios.post('/admin/sessions', { name: name || null, password }, withCredentials)),

  /** Everyone who had unlocked the session has to enter the new password. */
  changeSessionPassword: (sessionId: string, password: string) =>
    unwrap<void>(axios.put(`/admin/sessions/${sessionId}/password`, { password }, withCredentials)),

  renameSession: (sessionId: string, name: string) =>
    unwrap<void>(axios.patch(`/admin/sessions/${sessionId}`, { name }, withCredentials)),

  /** Also deletes the session's queue and uploaded photos. */
  deleteSession: (sessionId: string) =>
    unwrap<void>(axios.delete(`/admin/sessions/${sessionId}`, withCredentials)),

  /** Asks the host's player to pause or resume; rejects with status 409 when no player is connected. */
  sendPlayerCommand: (sessionId: string, command: PlayerCommand) =>
    unwrap<void>(axios.post(`/admin/sessions/${sessionId}/player/commands`, { command }, withCredentials)),

  /**
   * Skips to the next song, which the host's player then picks up. Rejects with status 409 when no player is
   * connected and 404 when the queue is empty. `currentSongId` is the song the admin saw playing.
   */
  skipSong: (sessionId: string, currentSongId?: number) =>
    unwrap<SongDto>(
      axios.post(`/admin/sessions/${sessionId}/songs/next`, null, { ...withCredentials, params: { currentSongId } }),
    ),

  getSlideshowSettings: (sessionId: string) =>
    unwrap<SlideshowSettingsDto>(axios.get(`/admin/sessions/${sessionId}/slideshow-settings`, withCredentials)),

  updateSlideshowSettings: (sessionId: string, settings: SlideshowSettingsDto) =>
    unwrap<void>(axios.put(`/admin/sessions/${sessionId}/slideshow-settings`, settings, withCredentials)),

  /** Removes the host so someone else can claim the session. */
  releaseHost: (sessionId: string) =>
    unwrap<void>(axios.delete(`/admin/sessions/${sessionId}/host`, withCredentials)),
};
