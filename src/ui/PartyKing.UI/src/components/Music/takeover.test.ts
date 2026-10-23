import { PlaybackState } from '@/service/endpoints/spotify/spotifyPlaybackApi';
import { takeoverPositionMs } from './takeover';

const URI = 'spotify:track:current';

const state = (overrides: Partial<PlaybackState> = {}): PlaybackState => ({
  deviceId: 'phone',
  uri: URI,
  isPlaying: true,
  progressMs: 10_000,
  timestamp: 1_000_000,
  ...overrides,
});

describe('takeoverPositionMs', () => {
  it('continues a song playing elsewhere from where it is by now', () => {
    expect(takeoverPositionMs(state(), 'web', URI, 1_002_500)).toBe(12_500);
  });

  it('continues a paused song from where it stopped, however old the reading', () => {
    expect(takeoverPositionMs(state({ isPlaying: false }), 'web', URI, 1_060_000)).toBe(10_000);
  });

  it('does not run backwards when the clocks disagree', () => {
    expect(takeoverPositionMs(state(), 'web', URI, 999_000)).toBe(10_000);
  });

  it.each([
    ['nothing is playing', null],
    ['the song already plays here', state({ deviceId: 'web' })],
    ['another song plays elsewhere', state({ uri: 'spotify:track:other' })],
    ['something that is not a track plays', state({ uri: null })],
    ['Spotify hides the device', state({ deviceId: null })],
  ])('has nothing to take over when %s', (_, playback) => {
    expect(takeoverPositionMs(playback, 'web', URI)).toBeNull();
  });
});
