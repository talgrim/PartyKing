import { act, render, waitFor } from '@testing-library/react';
import { createRef } from 'react';
import { SpotifyPlayer, SpotifyPlayerHandle } from './SpotifyPlayer';

vi.mock('@/service/endpoints/queue/queueApi', () => ({
  queueApi: { getSpotifyToken: vi.fn().mockResolvedValue('token') },
}));

const TRACK = 'spotify:track:current';
const OTHER_TRACK = 'spotify:track:other';

type Listener = (payload: unknown) => void;

class FakePlayer {
  static instances: FakePlayer[] = [];

  listeners = new Map<string, Listener>();
  togglePlay = vi.fn().mockResolvedValue(undefined);
  activateElement = vi.fn().mockResolvedValue(undefined);
  connect = vi.fn().mockResolvedValue(true);
  disconnect = vi.fn();
  setVolume = vi.fn().mockResolvedValue(undefined);

  constructor(public options: { volume?: number } = {}) {
    FakePlayer.instances.push(this);
  }

  addListener(event: string, callback: Listener) {
    this.listeners.set(event, callback);
    return true;
  }

  emit(event: string, payload: unknown) {
    act(() => this.listeners.get(event)?.(payload));
  }
}

const state = (paused: boolean, position: number, current: string, previous: string[] = []) => ({
  paused,
  position,
  track_window: {
    current_track: { uri: current, name: 'track' },
    previous_tracks: previous.map((uri) => ({ uri, name: 'track' })),
  },
});

const renderPlayer = async (uri: string | null = TRACK, volume?: number) => {
  const handlers = { onEnded: vi.fn(), onPausedChange: vi.fn(), onError: vi.fn() };
  const handle = createRef<SpotifyPlayerHandle>();
  const view = render(<SpotifyPlayer uri={uri} handle={handle} volume={volume} {...handlers} />);
  await waitFor(() => expect(FakePlayer.instances).toHaveLength(1));
  return { ...handlers, handle, view, player: FakePlayer.instances[0] };
};

describe('SpotifyPlayer', () => {
  beforeEach(() => {
    FakePlayer.instances = [];
    window.Spotify = { Player: FakePlayer } as unknown as typeof Spotify;
  });

  describe('volume', () => {
    it('starts at 50% by default', async () => {
      const { player } = await renderPlayer();

      expect(player.options.volume).toBe(0.5);
    });

    it('starts at the given volume', async () => {
      const { player } = await renderPlayer(TRACK, 0.8);

      expect(player.options.volume).toBe(0.8);
    });

    it('applies a changed volume to the SDK player', async () => {
      const { player, view, handle, ...handlers } = await renderPlayer(TRACK, 0.5);

      view.rerender(<SpotifyPlayer uri={TRACK} handle={handle} volume={0.7} {...handlers} />);

      expect(player.setVolume).toHaveBeenLastCalledWith(0.7);
    });

    it('applies the volume again when the device becomes ready', async () => {
      const { player } = await renderPlayer(TRACK, 0.3);

      player.emit('ready', { device_id: 'device-1' });

      await waitFor(() => expect(player.setVolume).toHaveBeenLastCalledWith(0.3));
    });
  });

  it('toggles playback of the SDK player through the handle', async () => {
    const { handle, player } = await renderPlayer();

    handle.current?.togglePlay();

    expect(player.togglePlay).toHaveBeenCalledTimes(1);
  });

  it('reports a pause and a resume of the current track', async () => {
    const { player, onPausedChange } = await renderPlayer();

    player.emit('player_state_changed', state(true, 42_000, TRACK));
    player.emit('player_state_changed', state(false, 42_000, TRACK));

    expect(onPausedChange.mock.calls).toEqual([[true], [false]]);
  });

  it('reports a pause at the very start of the track', async () => {
    const { player, onPausedChange, onEnded } = await renderPlayer();

    player.emit('player_state_changed', state(true, 0, TRACK));

    expect(onPausedChange).toHaveBeenCalledWith(true);
    expect(onEnded).not.toHaveBeenCalled();
  });

  it('ignores states of a track that is not the current one', async () => {
    const { player, onPausedChange } = await renderPlayer();

    player.emit('player_state_changed', state(true, 0, OTHER_TRACK));

    expect(onPausedChange).not.toHaveBeenCalled();
  });

  it('ignores states while no Spotify song is current', async () => {
    const { player, onPausedChange } = await renderPlayer(null);

    player.emit('player_state_changed', state(true, 1_000, TRACK));

    expect(onPausedChange).not.toHaveBeenCalled();
  });

  it('reports the end of a track once, without reporting it as a pause', async () => {
    const { player, onEnded, onPausedChange } = await renderPlayer();

    player.emit('player_state_changed', state(true, 0, TRACK, [TRACK]));
    player.emit('player_state_changed', state(true, 0, TRACK, [TRACK]));

    expect(onEnded).toHaveBeenCalledTimes(1);
    expect(onPausedChange).not.toHaveBeenCalled();
  });

  describe('playing a track', () => {
    const fetchMock = vi.fn();

    const playBodies = () => fetchMock.mock.calls.map(([, init]) => JSON.parse(init.body as string));

    beforeEach(() => {
      fetchMock.mockReset();
      fetchMock.mockResolvedValue({ ok: true });
      vi.stubGlobal('fetch', fetchMock);
    });

    afterEach(() => vi.unstubAllGlobals());

    it('starts from the beginning by default', async () => {
      const { player } = await renderPlayer();

      player.emit('ready', { device_id: 'web' });

      await waitFor(() => expect(playBodies()).toEqual([{ uris: [TRACK] }]));
    });

    it('continues from the position it takes the track over at, in the same call', async () => {
      const handlers = { onEnded: vi.fn(), onPausedChange: vi.fn(), onError: vi.fn() };
      render(<SpotifyPlayer uri={TRACK} takeover={{ uri: TRACK, positionMs: 42_000.4 }} {...handlers} />);
      await waitFor(() => expect(FakePlayer.instances).toHaveLength(1));

      FakePlayer.instances[0].emit('ready', { device_id: 'web' });

      await waitFor(() => expect(playBodies()).toEqual([{ uris: [TRACK], position_ms: 42_000 }]));
    });

    it('ignores a takeover meant for another track', async () => {
      const handlers = { onEnded: vi.fn(), onPausedChange: vi.fn(), onError: vi.fn() };
      render(<SpotifyPlayer uri={TRACK} takeover={{ uri: OTHER_TRACK, positionMs: 42_000 }} {...handlers} />);
      await waitFor(() => expect(FakePlayer.instances).toHaveLength(1));

      FakePlayer.instances[0].emit('ready', { device_id: 'web' });

      await waitFor(() => expect(playBodies()).toEqual([{ uris: [TRACK] }]));
    });

    it('reports its device id', async () => {
      const onDeviceChange = vi.fn();
      const handlers = { onEnded: vi.fn(), onPausedChange: vi.fn(), onError: vi.fn() };
      render(<SpotifyPlayer uri={null} onDeviceChange={onDeviceChange} {...handlers} />);
      await waitFor(() => expect(FakePlayer.instances).toHaveLength(1));

      FakePlayer.instances[0].emit('ready', { device_id: 'web' });
      FakePlayer.instances[0].emit('not_ready', { device_id: 'web' });

      expect(onDeviceChange.mock.calls).toEqual([['web'], [null]]);
    });
  });
});
