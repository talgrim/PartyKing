const PLAYER_URL = 'https://api.spotify.com/v1/me/player';

/** The part of Spotify's playback state that matters for taking playback over. */
export type PlaybackState = {
  /** The device playback is on; null when Spotify hides it (e.g. a private session). */
  deviceId: string | null;
  /** The track being played or paused; null for ads and episodes. */
  uri: string | null;
  isPlaying: boolean;
  progressMs: number;
  /** When Spotify took this reading, in epoch milliseconds. */
  timestamp: number;
};

type PlaybackStateResponse = {
  device?: { id?: string | null } | null;
  item?: { uri?: string } | null;
  is_playing?: boolean;
  progress_ms?: number | null;
  timestamp?: number;
};

/**
 * Reads what the host's account is playing on any of its devices. Resolves to null when nothing is playing and
 * rejects with `{ status }` otherwise, e.g. 403 when the token lacks the `user-read-playback-state` scope (a host
 * who logged in before that scope was requested).
 */
export const getPlaybackState = async (token: string): Promise<PlaybackState | null> => {
  const response = await fetch(PLAYER_URL, { headers: { Authorization: `Bearer ${token}` } });

  if (response.status === 204) return null;
  if (!response.ok) throw { status: response.status };

  const body = (await response.json()) as PlaybackStateResponse;
  return {
    deviceId: body.device?.id ?? null,
    uri: body.item?.uri ?? null,
    isPlaying: body.is_playing ?? false,
    progressMs: body.progress_ms ?? 0,
    timestamp: body.timestamp ?? Date.now(),
  };
};
