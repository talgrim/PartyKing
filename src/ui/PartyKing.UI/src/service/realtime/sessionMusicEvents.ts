import { SongDto } from '@/service/endpoints/queue/types';
import { LyricsDto } from '@/service/endpoints/lyrics/types';
import { NowPlayingDto, PlayerCommand } from '@/service/endpoints/player/types';

export const SESSION_MUSIC_EVENTS = {
  SongAdded: 'SongAdded',
  SongRemoved: 'SongRemoved',
  QueueAdvanced: 'QueueAdvanced',
  PlayerStateChanged: 'PlayerStateChanged',
  PlayerCommand: 'PlayerCommand',
  FallbackErrorChanged: 'FallbackErrorChanged',
  LyricsOffsetChanged: 'LyricsOffsetChanged',
  SlideChanged: 'SlideChanged',
  RingoChanged: 'RingoChanged',
  RingoOpened: 'RingoOpened',
  ResistanceChanged: 'ResistanceChanged',
  ResistanceOpened: 'ResistanceOpened',
  ResistanceLobbyChanged: 'ResistanceLobbyChanged',
  ResistancePresenceChanged: 'ResistancePresenceChanged',
} as const;

/**
 * What a connection listens to; the server puts it in one group per topic, so a page gets only the pushes it asked
 * for. `music` is the queue, the player, the fallback playlist status and the lyrics offsets. `resistance` is every
 * action of a Resistance round, `resistanceLobby` only what the lobby shows (the game exists, opens, starts, ends,
 * a player joins or is kicked), so a page that only offers the game does not wake up for every vote.
 * `resistancePresence` is who has the Resistance page open, pushed with the list in the payload; only the TV view asks.
 */
export const SESSION_HUB_TOPICS = [
  'music',
  'slideshow',
  'ringo',
  'resistance',
  'resistanceLobby',
  'resistancePresence',
] as const;

export type SessionHubTopic = (typeof SESSION_HUB_TOPICS)[number];

/**
 * The session page hears everything except the Resistance rounds: it needs the lobby pushes for the invite and the
 * games menu, and a vote must not make a page that only shows music fetch anything.
 */
export const MUSIC_QUEUE_HUB_TOPICS: readonly SessionHubTopic[] = ['music', 'slideshow', 'ringo', 'resistanceLobby'];

/** The Resistance board shows both the game and the lobby (the invite, who joined, the kick). */
export const RESISTANCE_HUB_TOPICS: readonly SessionHubTopic[] = ['resistance', 'resistanceLobby'];

/** The TV view also follows who is connected; the phones never do, so a blinking signal wakes nobody else. */
export const RESISTANCE_TV_HUB_TOPICS: readonly SessionHubTopic[] = [
  'resistance',
  'resistanceLobby',
  'resistancePresence',
];

/** The join page shows the lobby only; once the player joined the board opens its own connection. */
export const RESISTANCE_JOIN_HUB_TOPICS: readonly SessionHubTopic[] = ['resistanceLobby'];

export const RINGO_JOIN_HUB_TOPICS: readonly SessionHubTopic[] = ['ringo'];

/** Hub methods the client calls: tells the server this connection displays the slideshow (its timer runs only then). */
export const SESSION_HUB_METHODS = {
  WatchSlideshow: 'WatchSlideshow',
  StopWatchingSlideshow: 'StopWatchingSlideshow',
} as const;

export type SongRemovedEvent = { songId: number };

export type QueueAdvancedEvent = {
  previousSongId: number | null;
  nowPlaying: SongDto | null;
};

export type PlayerStateChangedEvent = {
  songId: number | null;
  paused: boolean;
  playerConnected: boolean;
  positionMs?: number;
};

export type PlayerCommandEvent = { command: PlayerCommand };

export type FallbackErrorChangedEvent = { error: string | null };

/** What the host's player does with the pushes only it cares about; set by the player, called by the hub hook. */
export type HostPlayerPushHandlers = {
  onCommand?: (command: PlayerCommand) => void;
  onFallbackError?: (error: string | null) => void;
  onReconnected?: () => void;
};

export const queueSongsKey = (sessionId: string) => ['queue-songs', sessionId] as const;
export const nowPlayingKey = (sessionId: string) => ['now-playing', sessionId] as const;
export const lyricsKey = (sessionId: string, songId: number) => ['lyrics', sessionId, songId] as const;
export const ringoKey = (sessionId: string) => ['ringo', sessionId] as const;
export const ringoLobbyKey = (sessionId: string) => ['ringo-lobby', sessionId] as const;
export const resistanceKey = (sessionId: string) => ['resistance', sessionId] as const;
export const resistanceLobbyKey = (sessionId: string) => ['resistance-lobby', sessionId] as const;
/** The seats of a started game a guest may take over; follows the lobby pushes (a seat released, claimed or its player back). */
export const resistanceClaimableSeatsKey = (sessionId: string) => ['resistance-claimable-seats', sessionId] as const;
export const resistancePresenceKey = (sessionId: string) => ['resistance-presence', sessionId] as const;
export const slideshowKey = (sessionId: string) => ({ endpoint: `/sessions/${sessionId}/photos/current` });

/** Puts a saved offset into the lyrics of the song it was saved for; lyrics that aren't loaded stay as they are. */
export const applyLyricsOffsetChanged = (
  current: LyricsDto | null | undefined,
  offsetMs: number,
): LyricsDto | null | undefined => (current ? { ...current, offsetMs } : current);

export const applySongAdded = (queue: SongDto[] | undefined, song: SongDto): SongDto[] => {
  if (!queue) return [song];
  return queue.some((item) => item.id === song.id) ? queue : [...queue, song];
};

export const applySongRemoved = (queue: SongDto[] | undefined, songId: number): SongDto[] | undefined =>
  queue?.filter((item) => item.id !== songId);

export const applyQueueAdvancedToQueue = (
  queue: SongDto[] | undefined,
  event: QueueAdvancedEvent,
): SongDto[] | undefined => {
  if (!queue) return queue;
  const dropIds = new Set(
    [event.previousSongId, event.nowPlaying?.id].filter((id): id is number => id != null),
  );
  return queue.filter((item) => !dropIds.has(item.id));
};

export const applyQueueAdvancedToNowPlaying = (
  current: NowPlayingDto | undefined,
  event: QueueAdvancedEvent,
): NowPlayingDto => ({
  song: event.nowPlaying,
  paused: false,
  playerConnected: current?.playerConnected ?? false,
  positionMs: 0,
});

export const applyPlayerStateChanged = (
  current: NowPlayingDto | undefined,
  event: PlayerStateChangedEvent,
): NowPlayingDto => {
  const song = current?.song ?? null;
  const paused = song != null && event.songId === song.id && event.paused;
  return {
    song,
    paused,
    playerConnected: event.playerConnected,
    positionMs: song != null && event.songId === song.id ? (event.positionMs ?? 0) : 0,
  };
};
