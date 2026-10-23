import { Ref, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { queueApi } from '@/service/endpoints/queue/queueApi';

export type SpotifyPlayerHandle = {
  /** Must be called from a user gesture (click) so the browser allows audio playback. */
  activate: () => void;
};

type Props = {
  /** Spotify track URI to play, or null when nothing should play. */
  uri: string | null;
  onEnded: () => void;
  onError: (message: string) => void;
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

export const SpotifyPlayer = ({ uri, onEnded, onError, handle }: Props) => {
  const playerRef = useRef<Spotify.Player | null>(null);
  const [deviceId, setDeviceId] = useState<string | null>(null);

  // The SDK callbacks are registered once, so keep the latest props in refs.
  const onEndedRef = useRef(onEnded);
  const onErrorRef = useRef(onError);
  const currentUriRef = useRef<string | null>(uri);
  const endedUriRef = useRef<string | null>(null);
  onEndedRef.current = onEnded;
  onErrorRef.current = onError;
  currentUriRef.current = uri;

  useImperativeHandle(handle, () => ({
    activate: () => {
      void playerRef.current?.activateElement();
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

      player.addListener('ready', ({ device_id }) => setDeviceId(device_id));
      player.addListener('not_ready', () => setDeviceId(null));
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

        if (finished && endedUriRef.current !== current) {
          endedUriRef.current = current;
          onEndedRef.current();
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
            body: JSON.stringify({ uris: [uri] }),
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
