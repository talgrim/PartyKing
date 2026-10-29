import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { MutableRefObject, useEffect, useState } from 'react';
import { mutate } from 'swr';
import envVariables from '@/envVariables';
import { LyricsDto, LyricsOffsetChangedEvent } from '@/service/endpoints/lyrics/types';
import { SongDto } from '@/service/endpoints/queue/types';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import {
  applyLyricsOffsetChanged,
  applyPlayerStateChanged,
  applyQueueAdvancedToNowPlaying,
  applyQueueAdvancedToQueue,
  applySongAdded,
  applySongRemoved,
  FallbackErrorChangedEvent,
  HostPlayerPushHandlers,
  lyricsKey,
  nowPlayingKey,
  PlayerCommandEvent,
  PlayerStateChangedEvent,
  QueueAdvancedEvent,
  queueSongsKey,
  ringoKey,
  SESSION_MUSIC_EVENTS,
  slideshowKey,
  SongRemovedEvent,
} from '@/service/realtime/sessionMusicEvents';

const hubUrl = (sessionId: string) => {
  const base = envVariables.apiUrl.replace(/\/$/, '');
  return `${base}/hubs/session?sessionId=${encodeURIComponent(sessionId)}`;
};

const revalidateSession = (sessionId: string) => {
  void mutate(queueSongsKey(sessionId));
  void mutate(nowPlayingKey(sessionId));
  void mutate(slideshowKey(sessionId));
  void mutate(ringoKey(sessionId));
  // A saved offset may have been missed meanwhile; only the lyrics of a mounted view are fetched again
  void mutate((key) => Array.isArray(key) && key[0] === 'lyrics' && key[1] === sessionId);
};

/** The live connection of a session page; `connected` is false while it is starting, reconnecting or down. */
export type SessionHubHandle = {
  connection: HubConnection | undefined;
  connected: boolean;
};

const DISCONNECTED: SessionHubHandle = { connection: undefined, connected: false };

/**
 * Subscribes to live queue / now-playing deltas for an unlocked session. Initial data still comes from SWR GETs;
 * this hook only patches the shared cache and revalidates after reconnect. Returns the connection so a view can
 * tell the server what it displays.
 */
export const useSessionMusicHub = (
  sessionId: string | undefined,
  enabled: boolean,
  hostPlayer?: MutableRefObject<HostPlayerPushHandlers>,
): SessionHubHandle => {
  const [handle, setHandle] = useState<SessionHubHandle>(DISCONNECTED);

  useEffect(() => {
    if (!enabled || !sessionId) return;

    let cancelled = false;
    let connection: HubConnection | undefined;

    const start = async () => {
      connection = new HubConnectionBuilder()
        .withUrl(hubUrl(sessionId), { withCredentials: true })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on(SESSION_MUSIC_EVENTS.SongAdded, (song: SongDto) => {
        void mutate(queueSongsKey(sessionId), (queue: SongDto[] | undefined) => applySongAdded(queue, song), {
          revalidate: false,
        });
      });

      connection.on(SESSION_MUSIC_EVENTS.SongRemoved, (event: SongRemovedEvent) => {
        void mutate(
          queueSongsKey(sessionId),
          (queue: SongDto[] | undefined) => applySongRemoved(queue, event.songId),
          { revalidate: false },
        );
      });

      connection.on(SESSION_MUSIC_EVENTS.QueueAdvanced, (event: QueueAdvancedEvent) => {
        void mutate(
          queueSongsKey(sessionId),
          (queue: SongDto[] | undefined) => applyQueueAdvancedToQueue(queue, event),
          { revalidate: false },
        );
        void mutate(
          nowPlayingKey(sessionId),
          (current: NowPlayingDto | undefined) => applyQueueAdvancedToNowPlaying(current, event),
          { revalidate: false },
        );
      });

      connection.on(SESSION_MUSIC_EVENTS.PlayerStateChanged, (event: PlayerStateChangedEvent) => {
        void mutate(
          nowPlayingKey(sessionId),
          (current: NowPlayingDto | undefined) => applyPlayerStateChanged(current, event),
          { revalidate: false },
        );
      });

      connection.on(SESSION_MUSIC_EVENTS.PlayerCommand, (event: PlayerCommandEvent) => {
        hostPlayer?.current.onCommand?.(event.command);
      });

      connection.on(SESSION_MUSIC_EVENTS.FallbackErrorChanged, (event: FallbackErrorChangedEvent) => {
        hostPlayer?.current.onFallbackError?.(event.error);
      });

      connection.on(SESSION_MUSIC_EVENTS.LyricsOffsetChanged, (event: LyricsOffsetChangedEvent) => {
        void mutate(
          lyricsKey(sessionId, event.songId),
          (current: LyricsDto | null | undefined) => applyLyricsOffsetChanged(current, event.offsetMs),
          { revalidate: false },
        );
      });

      connection.on(SESSION_MUSIC_EVENTS.SlideChanged, () => {
        void mutate(slideshowKey(sessionId));
      });

      connection.on(SESSION_MUSIC_EVENTS.RingoChanged, () => {
        void mutate(ringoKey(sessionId));
      });

      const built = connection;
      connection.onreconnecting(() => setHandle({ connection: built, connected: false }));
      connection.onclose(() => setHandle({ connection: built, connected: false }));
      connection.onreconnected(() => {
        revalidateSession(sessionId);
        hostPlayer?.current.onReconnected?.();
        setHandle({ connection: built, connected: true });
      });

      try {
        await connection.start();
        if (!cancelled) setHandle({ connection: built, connected: true });
      } catch {
        // Silent: REST still works; automatic reconnect will retry. Revalidate once we are up.
        if (!cancelled) revalidateSession(sessionId);
      }
    };

    void start();

    return () => {
      cancelled = true;
      const active = connection;
      connection = undefined;
      if (active && active.state !== HubConnectionState.Disconnected) {
        void active.stop();
      }
      setHandle(DISCONNECTED);
    };
  }, [sessionId, enabled, hostPlayer]);

  return handle;
};
