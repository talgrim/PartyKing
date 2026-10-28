import { SongSource } from '@/service/endpoints/queue/types';
import {
  applyPlayerStateChanged,
  applyQueueAdvancedToNowPlaying,
  applyQueueAdvancedToQueue,
  applySongAdded,
  applySongRemoved,
} from './sessionMusicEvents';

const song = (id: number, name = `Song ${id}`) => ({
  id,
  source: SongSource.Spotify,
  url: `spotify:track:${id}`,
  name,
  artists: 'Artist',
  albumArtUrl: null,
  durationMs: 1000,
});

describe('sessionMusicEvents', () => {
  it('appends a song once', () => {
    const first = song(1);
    expect(applySongAdded(undefined, first)).toEqual([first]);
    expect(applySongAdded([first], first)).toEqual([first]);
    expect(applySongAdded([first], song(2))).toEqual([first, song(2)]);
  });

  it('removes a queued song by id', () => {
    expect(applySongRemoved([song(1), song(2)], 1)).toEqual([song(2)]);
    expect(applySongRemoved(undefined, 1)).toBeUndefined();
  });

  it('drops the previous and newly playing songs from the pending list', () => {
    const queue = [song(2), song(3)];
    expect(
      applyQueueAdvancedToQueue(queue, { previousSongId: 1, nowPlaying: song(2) }),
    ).toEqual([song(3)]);
  });

  it('replaces now-playing on advance and clears pause', () => {
    expect(
      applyQueueAdvancedToNowPlaying(
        { song: song(1), paused: true, playerConnected: true },
        { previousSongId: 1, nowPlaying: song(2) },
      ),
    ).toEqual({ song: song(2), paused: false, playerConnected: true });
  });

  it('applies pause only when the reported song is the one playing', () => {
    const current = { song: song(2), paused: false, playerConnected: true };
    expect(applyPlayerStateChanged(current, { songId: 2, paused: true, playerConnected: true })).toEqual({
      song: song(2),
      paused: true,
      playerConnected: true,
    });
    expect(applyPlayerStateChanged(current, { songId: 1, paused: true, playerConnected: true })).toEqual({
      song: song(2),
      paused: false,
      playerConnected: true,
    });
  });
});
