import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { MutableRefObject, useEffect } from 'react';
import { mutate } from 'swr';
import envVariables from '@/envVariables';
import { SongDto } from '@/service/endpoints/queue/types';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import {
  applyPlayerStateChanged,
  applyQueueAdvancedToNowPlaying,
  applyQueueAdvancedToQueue,
  applySongAdded,
  applySongRemoved,
  FallbackErrorChangedEvent,
  HostPlayerPushHandlers,
  nowPlayingKey,
  PlayerCommandEvent,
  PlayerStateChangedEvent,
  QueueAdvancedEvent,
  queueSongsKey,
  SESSION_MUSIC_EVENTS,
  SongRemovedEvent,
} from '@/service/realtime/sessionMusicEvents';

const hubUrl = (sessionId: string) => {
  const base = envVariables.apiUrl.replace(/\/$/, '');
  return `${base}/hubs/session?sessionId=${encodeURIComponent(sessionId)}`;
};

const revalidateMusic = (sessionId: string) => {
  void mutate(queueSongsKey(sessionId));
  void mutate(nowPlayingKey(sessionId));
};

/**
 * Subscribes to live queue / now-playing deltas for an unlocked session. Initial data still comes from SWR GETs;
 * this hook only patches the shared cache and revalidates after reconnect.
 */
export const useSessionMusicHub = (
  sessionId: string | undefined,
  enabled: boolean,
  hostPlayer?: MutableRefObject<HostPlayerPushHandlers>,
): void => {
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

      connection.onreconnected(() => {
        revalidateMusic(sessionId);
        hostPlayer?.current.onReconnected?.();
      });

      try {
        await connection.start();
      } catch {
        // Silent: REST still works; automatic reconnect will retry. Revalidate once we are up.
        if (!cancelled) revalidateMusic(sessionId);
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
    };
  }, [sessionId, enabled, hostPlayer]);
};
