// Minimal typings for the parts of the Spotify Web Playback SDK that the app uses.
declare namespace Spotify {
  interface Track {
    uri: string;
    name: string;
  }

  interface PlaybackState {
    paused: boolean;
    position: number;
    track_window: {
      current_track: Track;
      previous_tracks: Track[];
    };
  }

  interface Error {
    message: string;
  }

  interface PlayerInit {
    name: string;
    volume?: number;
    getOAuthToken: (callback: (token: string) => void) => void;
  }

  class Player {
    constructor(options: PlayerInit);
    connect(): Promise<boolean>;
    disconnect(): void;
    activateElement(): Promise<void>;
    togglePlay(): Promise<void>;
    pause(): Promise<void>;
    setVolume(volume: number): Promise<void>;
    getCurrentState(): Promise<PlaybackState | null>;
    addListener(event: 'ready' | 'not_ready', callback: (data: { device_id: string }) => void): boolean;
    addListener(
      event: 'initialization_error' | 'authentication_error' | 'account_error' | 'playback_error',
      callback: (error: Error) => void,
    ): boolean;
    addListener(event: 'player_state_changed', callback: (state: PlaybackState | null) => void): boolean;
  }
}

interface Window {
  Spotify: typeof Spotify;
  onSpotifyWebPlaybackSDKReady: () => void;
}
