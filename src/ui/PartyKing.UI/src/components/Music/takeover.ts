import useSWR from 'swr';
import { getPlaybackState, PlaybackState } from '@/service/endpoints/spotify/spotifyPlaybackApi';

const PLAYBACK_REFRESH_MS = 3000;

/**
 * Where to continue `uri` when this browser takes it over from another of the host's devices, or null when there is
 * nothing to take over: that song is not the one on another device. A paused song counts too, so it is not restarted.
 */
export const takeoverPositionMs = (
  state: PlaybackState | null | undefined,
  ownDeviceId: string,
  uri: string,
  now = Date.now(),
): number | null => {
  if (!state || state.uri !== uri || state.deviceId === null || state.deviceId === ownDeviceId) return null;

  // A reading of a playing song is already out of date by the time it is used
  return state.isPlaying ? state.progressMs + Math.max(0, now - state.timestamp) : state.progressMs;
};

/** Whether `uri` is on another device of the host right now. Polls while `enabled`. */
export const useTakeoverAvailable = (
  token: string | undefined,
  ownDeviceId: string | null,
  uri: string | null,
  enabled: boolean,
): boolean => {
  const { data } = useSWR(
    enabled && token && ownDeviceId && uri ? ['spotify-playback', ownDeviceId] : null,
    () => getPlaybackState(token!),
    {
      refreshInterval: PLAYBACK_REFRESH_MS,
      revalidateOnFocus: false,
      // A host whose token lacks the scope gets 403: just no takeover, the normal button stays
      shouldRetryOnError: false,
    },
  );

  return !!ownDeviceId && !!uri && takeoverPositionMs(data, ownDeviceId, uri) !== null;
};
