import { Ref, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { queueApi } from '@/service/endpoints/queue/queueApi';

export type SpotifyPlayerHandle = {
  /** Must be called from a user gesture (click) so the browser allows audio playback. */
  activate: () => void;
  /** Pauses the current track, or resumes it when it is paused. */
  togglePlay: () => void;
};

type Props = {
  /** Spotify track URI to play, or null when nothing should play. */
  uri: string | null;
  onEnded: () => void;
  /** Reports the real playback state of `uri`, including pauses made from other Spotify clients. */
  onPausedChange: (paused: boolean) => void;
  onError: (message: string) => void;
  /** Reports the id of this browser's Spotify device once it is ready, and null when it goes away. */
  onDeviceChange?: (deviceId: string | null) => void;
  /** Continue `uri` from `positionMs` instead of from the start, once, when it is taken over from another device. */
  takeover?: { uri: string; positionMs: number } | null;
  handle?: Ref<SpotifyPlayerHandle>;
};

const SDK_URL = 'https://sdk.scdn.co/spotify-player.js';

let sdkPromise: Promise<void> | null = null;

const loadSdk = () => {
  sdkPromise ??= new Promise<void>((resolve) => {
    if (window.Spotify) {
      resolve();
      return;
    }
    window.onSpotifyWebPlaybackSDKReady = () => resolve();
    const script = document.createElement('script');
    script.src = SDK_URL;
    script.async = true;
    document.body.appendChild(script);
  });
  return sdkPromise;
};

export const SpotifyPlayer = ({
  uri,
  onEnded,
  onPausedChange,
  onError,
  onDeviceChange,
  takeover,
  handle,
}: Props) => {
  const playerRef = useRef<Spotify.Player | null>(null);
  const [deviceId, setDeviceId] = useState<string | null>(null);

  // The SDK callbacks are registered once, so keep the latest props in refs.
  const onEndedRef = useRef(onEnded);
  const onPausedChangeRef = useRef(onPausedChange);
  const onErrorRef = useRef(onError);
  const onDeviceChangeRef = useRef(onDeviceChange);
  const takeoverRef = useRef(takeover);
  const usedTakeoverRef = useRef<Props['takeover']>(null);
  const currentUriRef = useRef<string | null>(uri);
  const endedUriRef = useRef<string | null>(null);
  onEndedRef.current = onEnded;
  onPausedChangeRef.current = onPausedChange;
  onErrorRef.current = onError;
  onDeviceChangeRef.current = onDeviceChange;
  takeoverRef.current = takeover;
  currentUriRef.current = uri;

  useImperativeHandle(handle, () => ({
    activate: () => {
      void playerRef.current?.activateElement();
    },
    togglePlay: () => {
      void playerRef.current?.togglePlay();
    },
  }));

  useEffect(() => {
    let disposed = false;
    let player: Spotify.Player | null = null;

    loadSdk().then(() => {
      if (disposed) return;

      player = new window.Spotify.Player({
        name: 'PartyKing',
        volume: 0.5,
        getOAuthToken: (callback) => {
          queueApi
            .getSpotifyToken()
            .then(callback)
            .catch(() => onErrorRef.current('Could not get a Spotify token. Log in again.'));
        },
      });

      player.addListener('ready', ({ device_id }) => {
        setDeviceId(device_id);
        onDeviceChangeRef.current?.(device_id);
      });
      player.addListener('not_ready', () => {
        setDeviceId(null);
        onDeviceChangeRef.current?.(null);
      });
      player.addListener('initialization_error', ({ message }) => onErrorRef.current(message));
      player.addListener('authentication_error', ({ message }) => onErrorRef.current(message));
      player.addListener('account_error', () =>
        onErrorRef.current('Spotify Premium is required to play music in the browser.'),
      );
      player.addListener('playback_error', ({ message }) => onErrorRef.current(message));

      player.addListener('player_state_changed', (state) => {
        const current = currentUriRef.current;
        if (!state || !current) return;

        // A track has finished when playback stops at position 0 and it moved into the "previous" list.
        const finished =
          state.paused &&
          state.position === 0 &&
          state.track_window.previous_tracks.some((track) => track.uri === current);

        if (finished) {
          if (endedUriRef.current !== current) {
            endedUriRef.current = current;
            onEndedRef.current();
          }
          return;
        }

        // Ignore states of a previous track that arrive after the next one was requested.
        if (state.track_window.current_track.uri === current) {
          onPausedChangeRef.current(state.paused);
        }
      });

      playerRef.current = player;
      void player.connect();
    });

    return () => {
      disposed = true;
      player?.disconnect();
      playerRef.current = null;
    };
  }, []);

  useEffect(() => {
    if (!uri || !deviceId) return;

    let cancelled = false;

    const play = async () => {
      // Used up by this call: a later replay of the song (e.g. after a reconnect) starts from the beginning
      const pending = takeoverRef.current;
      const resume =
        pending && pending !== usedTakeoverRef.current && pending.uri === uri ? pending.positionMs : undefined;
      usedTakeoverRef.current = pending;

      try {
        const token = await queueApi.getSpotifyToken();
        const response = await fetch(
          `https://api.spotify.com/v1/me/player/play?device_id=${encodeURIComponent(deviceId)}`,
          {
            method: 'PUT',
            headers: {
              Authorization: `Bearer ${token}`,
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({ uris: [uri], ...(resume !== undefined && { position_ms: Math.round(resume) }) }),
          },
        );

        if (!cancelled && !response.ok) {
          onErrorRef.current(`Spotify refused to play the track (${response.status})`);
        }
      } catch {
        if (!cancelled) onErrorRef.current('Could not start Spotify playback');
      }
    };

    void play();

    return () => {
      cancelled = true;
    };
  }, [uri, deviceId]);

  return null;
};
