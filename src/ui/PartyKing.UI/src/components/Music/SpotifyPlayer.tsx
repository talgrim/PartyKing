import { Ref, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { queueApi } from '@/service/endpoints/queue/queueApi';
import { i18n } from '@/i18n';

export type SpotifyPlayerHandle = {
  /** Must be called from a user gesture (click) so the browser allows audio playback. */
  activate: () => void;
  /** Pauses the current track, or resumes it when it is paused. */
  togglePlay: () => void;
  /** Where in the track the player is right now, or null when the SDK has no state. */
  getPositionMs: () => Promise<number | null>;
};

type Props = {
  /** Spotify track URI to play, or null when nothing should play. */
  uri: string | null;
  /** Id of the queue item being played. The same track can be queued again (e.g. a one-track fallback playlist), so `uri` alone can't tell songs apart. */
  songId: number | null;
  onEnded: () => void;
  /** Reports the real playback state of `uri`, including pauses made from other Spotify clients. */
  onPausedChange: (paused: boolean) => void;
  /** Reports where in `uri` the player is every time the SDK reports a state of it, to notice seeks. */
  onPositionChange?: (positionMs: number, paused: boolean) => void;
  onError: (message: string) => void;
  /** Reports the id of this browser's Spotify device once it is ready, and null when it goes away. */
  onDeviceChange?: (deviceId: string | null) => void;
  /** Continue `uri` from `positionMs` instead of from the start, once, when it is taken over from another device. */
  takeover?: { uri: string; positionMs: number } | null;
  handle?: Ref<SpotifyPlayerHandle>;
  /** 0 to 1. */
  volume?: number;
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
  songId,
  onEnded,
  onPausedChange,
  onPositionChange,
  onError,
  onDeviceChange,
  takeover,
  handle,
  volume = 0.5,
}: Props) => {
  const playerRef = useRef<Spotify.Player | null>(null);
  const [deviceId, setDeviceId] = useState<string | null>(null);

  // The SDK callbacks are registered once, so keep the latest props in refs.
  const onEndedRef = useRef(onEnded);
  const onPausedChangeRef = useRef(onPausedChange);
  const onPositionChangeRef = useRef(onPositionChange);
  const onErrorRef = useRef(onError);
  const onDeviceChangeRef = useRef(onDeviceChange);
  const takeoverRef = useRef(takeover);
  const usedTakeoverRef = useRef<Props['takeover']>(null);
  const currentUriRef = useRef<string | null>(uri);
  const currentSongIdRef = useRef<number | null>(songId);
  const endedSongIdRef = useRef<number | null>(null);
  const volumeRef = useRef(volume);
  volumeRef.current = volume;
  onEndedRef.current = onEnded;
  onPausedChangeRef.current = onPausedChange;
  onPositionChangeRef.current = onPositionChange;
  onErrorRef.current = onError;
  onDeviceChangeRef.current = onDeviceChange;
  takeoverRef.current = takeover;
  currentUriRef.current = uri;
  currentSongIdRef.current = songId;

  useImperativeHandle(handle, () => ({
    activate: () => {
      void playerRef.current?.activateElement();
    },
    togglePlay: () => {
      void playerRef.current?.togglePlay();
    },
    getPositionMs: async () => {
      const state = await playerRef.current?.getCurrentState?.();
      return state ? state.position : null;
    },
  }));

  // The device id changes when the SDK connects, so re-apply the volume then as well.
  useEffect(() => {
    void playerRef.current?.setVolume(volume);
  }, [volume, deviceId]);

  useEffect(() => {
    let disposed = false;
    let player: Spotify.Player | null = null;

    loadSdk().then(() => {
      if (disposed) return;

      player = new window.Spotify.Player({
        name: 'PartyKing',
        volume: volumeRef.current,
        getOAuthToken: (callback) => {
          queueApi
            .getSpotifyToken()
            .then(callback)
            .catch(() => onErrorRef.current(i18n.t('spotifyPlayer.tokenFailed')));
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
        onErrorRef.current(i18n.t('spotifyPlayer.premium')),
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
          if (endedSongIdRef.current !== currentSongIdRef.current) {
            endedSongIdRef.current = currentSongIdRef.current;
            onEndedRef.current();
          }
          return;
        }

        // Ignore states of a previous track that arrive after the next one was requested.
        if (state.track_window.current_track.uri === current) {
          onPositionChangeRef.current?.(state.position, state.paused);
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

  // The song moved on to something that is not a Spotify track (a YouTube video, or nothing): the SDK keeps playing
  // the old track on its own, so stop it.
  const hadUriRef = useRef(false);
  useEffect(() => {
    if (uri) {
      hadUriRef.current = true;
    } else if (hadUriRef.current) {
      hadUriRef.current = false;
      void playerRef.current?.pause();
    }
  }, [uri]);

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
          onErrorRef.current(i18n.t('spotifyPlayer.refused', { status: response.status }));
        }
      } catch {
        if (!cancelled) onErrorRef.current(i18n.t('spotifyPlayer.startFailed'));
      }
    };

    void play();

    return () => {
      cancelled = true;
    };
  }, [uri, songId, deviceId]);

  return null;
};
