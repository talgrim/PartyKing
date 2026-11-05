import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { MutableRefObject, useEffect, useState } from 'react';
import { mutate } from 'swr';
import envVariables from '@/envVariables';
import { devPersona } from '@/service/devPersona';
import { ResistancePresenceDto } from '@/service/endpoints/games/types';
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
  resistanceClaimableSeatsKey,
  resistanceLobbyKey,
  resistancePresenceKey,
  ringoLobbyKey,
  SESSION_HUB_TOPICS,
  SESSION_MUSIC_EVENTS,
  SessionHubTopic,
  slideshowKey,
  SongRemovedEvent,
} from '@/service/realtime/sessionMusicEvents';

const hubUrl = (sessionId: string, topics: readonly SessionHubTopic[], presence: boolean) => {
  const base = envVariables.apiUrl.replace(/\/$/, '');
  let url = `${base}/hubs/session?sessionId=${encodeURIComponent(sessionId)}&topics=${topics.join(',')}`;
  // The connection of a player's own Resistance page counts as that player being online (the server reads the player from the cookie)
  if (presence) url += '&presence=1';
  // A hub connection can't send the dev persona header, so the development server reads it from the query
  if (devPersona) url += `&persona=${encodeURIComponent(devPersona)}`;
  return url;
};

/** A burst of pushes (everyone voting at once) is answered with one refetch per client instead of one per push. */
const RESISTANCE_REVALIDATE_DEBOUNCE_MS = 250;

/**
 * Each client waits a random extra moment, so a push doesn't make every client fetch in the same instant. It was 500 ms
 * while a fetch cost about 55 ms of server CPU (a hash built on every request, since fixed): the spread kept the
 * machine from being swamped, but a vote took up to 750 ms to show. A fetch is about 1 ms now, so 100 ms is enough.
 */
const RESISTANCE_REVALIDATE_JITTER_MS = 100;

/**
 * The lobby changes a few times per game, and `ResistanceOpened` and `ResistanceLobbyChanged` arrive together when an
 * admin opens it: a short wait makes them one fetch.
 */
const RESISTANCE_LOBBY_REVALIDATE_DEBOUNCE_MS = 50;
const RESISTANCE_LOBBY_REVALIDATE_JITTER_MS = 250;

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
  }
  if (topics.includes('resistanceLobby')) {
    void mutate(resistanceLobbyKey(sessionId));
    void mutate(resistanceClaimableSeatsKey(sessionId));
  }
  if (topics.includes('resistancePresence')) {
    void mutate(resistancePresenceKey(sessionId));
  }
};

export type SessionHubOptions = {
  /**
   * This connection is a Resistance player's own page: the server counts the player in the cookie as online while it
   * lasts. Leave it out on the TV view, which shares that cookie.
   */
  presence?: boolean;
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
  options: SessionHubOptions = {},
): SessionHubHandle => {
  const [handle, setHandle] = useState<SessionHubHandle>(DISCONNECTED);
  // A new array with the same topics must not restart the connection
  const topicKey = topics.join(',');
  const presence = options.presence === true;

  useEffect(() => {
    if (!enabled || !sessionId) return;

    const subscribed = topicKey.split(',') as SessionHubTopic[];
    let cancelled = false;
    let connection: HubConnection | undefined;
    let resistanceTimer: ReturnType<typeof setTimeout> | undefined;
    let resistanceLobbyTimer: ReturnType<typeof setTimeout> | undefined;

    const revalidateResistanceLobby = () => {
      clearTimeout(resistanceLobbyTimer);
      resistanceLobbyTimer = setTimeout(
        () => {
          void mutate(resistanceLobbyKey(sessionId));
          void mutate(resistanceClaimableSeatsKey(sessionId));
        },
        RESISTANCE_LOBBY_REVALIDATE_DEBOUNCE_MS + Math.random() * RESISTANCE_LOBBY_REVALIDATE_JITTER_MS,
      );
    };

    const start = async () => {
      connection = new HubConnectionBuilder()
        .withUrl(hubUrl(sessionId, subscribed, presence), { withCredentials: true })
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
        }, RESISTANCE_REVALIDATE_DEBOUNCE_MS + Math.random() * RESISTANCE_REVALIDATE_JITTER_MS);
      });

      connection.on(SESSION_MUSIC_EVENTS.ResistanceOpened, revalidateResistanceLobby);

      connection.on(SESSION_MUSIC_EVENTS.ResistanceLobbyChanged, revalidateResistanceLobby);

      connection.on(SESSION_MUSIC_EVENTS.ResistancePresenceChanged, (presenceNow: ResistancePresenceDto) => {
        void mutate(resistancePresenceKey(sessionId), presenceNow, { revalidate: false });
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
      clearTimeout(resistanceLobbyTimer);
      const active = connection;
      connection = undefined;
      if (active && active.state !== HubConnectionState.Disconnected) {
        void active.stop();
      }
      setHandle(DISCONNECTED);
    };
  }, [sessionId, enabled, hostPlayer, topicKey, presence]);

  return handle;
};
