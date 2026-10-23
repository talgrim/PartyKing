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

  constructor() {
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

const renderPlayer = async (uri: string | null = TRACK) => {
  const handlers = { onEnded: vi.fn(), onPausedChange: vi.fn(), onError: vi.fn() };
  const handle = createRef<SpotifyPlayerHandle>();
  render(<SpotifyPlayer uri={uri} handle={handle} {...handlers} />);
  await waitFor(() => expect(FakePlayer.instances).toHaveLength(1));
  return { ...handlers, handle, player: FakePlayer.instances[0] };
};

describe('SpotifyPlayer', () => {
  beforeEach(() => {
    FakePlayer.instances = [];
    window.Spotify = { Player: FakePlayer } as unknown as typeof Spotify;
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
});
