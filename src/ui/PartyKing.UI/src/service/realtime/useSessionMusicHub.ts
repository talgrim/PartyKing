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
  resistanceKey,
  resistanceLobbyKey,
  ringoLobbyKey,
  SESSION_HUB_TOPICS,
  SESSION_MUSIC_EVENTS,
  SessionHubTopic,
  slideshowKey,
  SongRemovedEvent,
} from '@/service/realtime/sessionMusicEvents';

const hubUrl = (sessionId: string, topics: readonly SessionHubTopic[]) => {
  const base = envVariables.apiUrl.replace(/\/$/, '');
  return `${base}/hubs/session?sessionId=${encodeURIComponent(sessionId)}&topics=${topics.join(',')}`;
};

/** A burst of pushes (everyone voting at once) is answered with one refetch per client instead of one per push. */
const RESISTANCE_REVALIDATE_DEBOUNCE_MS = 250;

/** Each client waits a random extra moment, so a push doesn't make every client fetch in the same instant. */
const RESISTANCE_REVALIDATE_JITTER_MS = 500;

/** Fetches again what the topics feed, after a missed push (reconnect, failed start). */
const revalidateSession = (sessionId: string, topics: readonly SessionHubTopic[]) => {
  if (topics.includes('music')) {
    void mutate(queueSongsKey(sessionId));
    void mutate(nowPlayingKey(sessionId));
    // A saved offset may have been missed meanwhile; only the lyrics of a mounted view are fetched again
    void mutate((key) => Array.isArray(key) && key[0] === 'lyrics' && key[1] === sessionId);
  }
  if (topics.includes('slideshow')) {
    void mutate(slideshowKey(sessionId));
  }
  if (topics.includes('ringo')) {
    void mutate(ringoKey(sessionId));
    void mutate(ringoLobbyKey(sessionId));
  }
  if (topics.includes('resistance')) {
    void mutate(resistanceKey(sessionId));
    void mutate(resistanceLobbyKey(sessionId));
  }
};

/** The live connection of a session page; `connected` is false while it is starting, reconnecting or down. */
export type SessionHubHandle = {
  connection: HubConnection | undefined;
  connected: boolean;
};

const DISCONNECTED: SessionHubHandle = { connection: undefined, connected: false };

/**
 * Subscribes to live deltas for an unlocked session. Initial data still comes from SWR GETs; this hook only patches
 * the shared cache and revalidates after reconnect. `topics` limits the pushes the connection gets (all by default),
 * so a game page does not hear about the queue. Returns the connection so a view can tell the server what it displays.
 */
export const useSessionMusicHub = (
  sessionId: string | undefined,
  enabled: boolean,
  hostPlayer?: MutableRefObject<HostPlayerPushHandlers>,
  topics: readonly SessionHubTopic[] = SESSION_HUB_TOPICS,
): SessionHubHandle => {
  const [handle, setHandle] = useState<SessionHubHandle>(DISCONNECTED);
  // A new array with the same topics must not restart the connection
  const topicKey = topics.join(',');

  useEffect(() => {
    if (!enabled || !sessionId) return;

    const subscribed = topicKey.split(',') as SessionHubTopic[];
    let cancelled = false;
    let connection: HubConnection | undefined;
    let resistanceTimer: ReturnType<typeof setTimeout> | undefined;

    const start = async () => {
      connection = new HubConnectionBuilder()
        .withUrl(hubUrl(sessionId, subscribed), { withCredentials: true })
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
        void mutate(ringoLobbyKey(sessionId));
      });

      connection.on(SESSION_MUSIC_EVENTS.RingoOpened, () => {
        void mutate(ringoLobbyKey(sessionId));
      });

      connection.on(SESSION_MUSIC_EVENTS.ResistanceChanged, () => {
        clearTimeout(resistanceTimer);
        resistanceTimer = setTimeout(() => {
          void mutate(resistanceKey(sessionId));
          void mutate(resistanceLobbyKey(sessionId));
        }, RESISTANCE_REVALIDATE_DEBOUNCE_MS + Math.random() * RESISTANCE_REVALIDATE_JITTER_MS);
      });

      connection.on(SESSION_MUSIC_EVENTS.ResistanceOpened, () => {
        void mutate(resistanceLobbyKey(sessionId));
      });

      const built = connection;
      connection.onreconnecting(() => setHandle({ connection: built, connected: false }));
      connection.onclose(() => setHandle({ connection: built, connected: false }));
      connection.onreconnected(() => {
        revalidateSession(sessionId, subscribed);
        hostPlayer?.current.onReconnected?.();
        setHandle({ connection: built, connected: true });
      });

      try {
        await connection.start();
        if (!cancelled) setHandle({ connection: built, connected: true });
      } catch {
        // Silent: REST still works; automatic reconnect will retry. Revalidate once we are up.
        if (!cancelled) revalidateSession(sessionId, subscribed);
      }
    };

    void start();

    return () => {
      cancelled = true;
      clearTimeout(resistanceTimer);
      const active = connection;
      connection = undefined;
      if (active && active.state !== HubConnectionState.Disconnected) {
        void active.stop();
      }
      setHandle(DISCONNECTED);
    };
  }, [sessionId, enabled, hostPlayer, topicKey]);

  return handle;
};
