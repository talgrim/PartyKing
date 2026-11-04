import { act, renderHook } from '@testing-library/react';
import { mutate } from 'swr';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import {
  MUSIC_QUEUE_HUB_TOPICS,
  RESISTANCE_HUB_TOPICS,
  RESISTANCE_JOIN_HUB_TOPICS,
  resistanceKey,
  resistanceLobbyKey,
  SESSION_HUB_TOPICS,
  SessionHubTopic,
} from '@/service/realtime/sessionMusicEvents';

vi.mock('swr', () => ({ mutate: vi.fn() }));

type Handler = (...args: unknown[]) => void;

const hub = vi.hoisted(() => ({
  urls: [] as string[],
  handlers: new Map<string, Handler>(),
  reconnected: undefined as Handler | undefined,
}));

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl(url: string) {
      hub.urls.push(url);
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    configureLogging() {
      return this;
    }
    build() {
      return {
        state: 'Connected',
        on: (name: string, handler: Handler) => hub.handlers.set(name, handler),
        onreconnecting: vi.fn(),
        onclose: vi.fn(),
        onreconnected: (handler: Handler) => {
          hub.reconnected = handler;
        },
        start: vi.fn().mockResolvedValue(undefined),
        stop: vi.fn().mockResolvedValue(undefined),
      };
    }
  }
  return {
    HubConnectionBuilder,
    HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
    LogLevel: { Warning: 'Warning' },
  };
});

const SESSION = 'session-1';
const mutated = () => vi.mocked(mutate).mock.calls.map(([key]) => key);

/** Mounts the hook and lets the connection start; the pushes are then sent through `hub.handlers`. */
const mountHub = async (topics?: readonly SessionHubTopic[]) => {
  const rendered = renderHook(() => useSessionMusicHub(SESSION, true, undefined, topics));
  await act(async () => {});
  return rendered;
};

const push = (name: string) => {
  act(() => hub.handlers.get(name)!());
};

const wait = async (ms: number) => {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
};

describe('useSessionMusicHub, Resistance pushes', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.mocked(mutate).mockClear();
    hub.urls.length = 0;
    hub.handlers.clear();
    hub.reconnected = undefined;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('refetches only the game on ResistanceChanged', async () => {
    await mountHub(RESISTANCE_HUB_TOPICS);

    push('ResistanceChanged');
    await wait(1000);

    expect(mutated()).toEqual([resistanceKey(SESSION)]);
  });

  it('refetches only the lobby on ResistanceLobbyChanged', async () => {
    await mountHub(RESISTANCE_HUB_TOPICS);

    push('ResistanceLobbyChanged');
    await wait(1000);

    expect(mutated()).toEqual([resistanceLobbyKey(SESSION)]);
  });

  it('turns ResistanceOpened and ResistanceLobbyChanged of one opening into one lobby fetch', async () => {
    await mountHub(RESISTANCE_HUB_TOPICS);

    push('ResistanceOpened');
    push('ResistanceChanged');
    push('ResistanceLobbyChanged');
    await wait(1000);

    expect(mutated().filter((key) => (key as string[])[0] === 'resistance-lobby')).toHaveLength(1);
    expect(mutated().filter((key) => (key as string[])[0] === 'resistance')).toHaveLength(1);
  });

  it('turns a burst of actions into one game fetch and no lobby fetch', async () => {
    await mountHub(RESISTANCE_HUB_TOPICS);

    for (let i = 0; i < 5; i++) {
      push('ResistanceChanged');
    }
    await wait(1000);

    expect(mutated()).toEqual([resistanceKey(SESSION)]);
  });

  it('fetches the lobby within a moment, not at the pace of the game', async () => {
    await mountHub(RESISTANCE_HUB_TOPICS);

    push('ResistanceLobbyChanged');
    await wait(300);

    expect(mutated()).toEqual([resistanceLobbyKey(SESSION)]);
  });

  it('asks the server for the topics it was given', async () => {
    await mountHub(MUSIC_QUEUE_HUB_TOPICS);

    expect(hub.urls[0]).toContain('topics=music,slideshow,ringo,resistanceLobby');
    expect(hub.urls[0]).not.toMatch(/resistance(,|$)/);
  });

  it('asks for every topic by default', async () => {
    await mountHub();

    expect(hub.urls[0]).toContain(`topics=${SESSION_HUB_TOPICS.join(',')}`);
  });

  it('fetches again after a reconnect only what its topics feed (the lobby page)', async () => {
    await mountHub(RESISTANCE_JOIN_HUB_TOPICS);

    act(() => hub.reconnected!());

    expect(mutated()).toEqual([resistanceLobbyKey(SESSION)]);
  });

  it('fetches the game and the lobby again after a reconnect of the board', async () => {
    await mountHub(RESISTANCE_HUB_TOPICS);

    act(() => hub.reconnected!());

    expect(mutated()).toEqual([resistanceKey(SESSION), resistanceLobbyKey(SESSION)]);
  });

  it('leaves a pending fetch undone when the page goes away', async () => {
    const { unmount } = await mountHub(RESISTANCE_HUB_TOPICS);

    push('ResistanceChanged');
    push('ResistanceLobbyChanged');
    unmount();
    await wait(1000);

    expect(mutated()).toEqual([]);
  });
});
