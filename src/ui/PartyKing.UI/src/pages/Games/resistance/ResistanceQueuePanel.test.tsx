import { render, screen } from '@testing-library/react';
import { ResistanceQueuePanel } from './ResistanceQueuePanel';

const mocks = vi.hoisted(() => ({
  role: { isHost: false, isAdmin: false },
  nowPlaying: { song: null, paused: false, playerConnected: true } as unknown,
  controls: vi.fn(),
  queue: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({ useViewerRole: () => mocks.role }));
vi.mock('@/service/endpoints/player/useNowPlaying', () => ({
  useNowPlaying: () => ({ data: mocks.nowPlaying, mutate: vi.fn() }),
}));
vi.mock('@/components/Music/NowPlayingLine', () => ({ NowPlayingLine: () => <div>now playing line</div> }));
vi.mock('@/components/Music/Queue', () => ({
  Queue: (props: unknown) => {
    mocks.queue(props);
    return <div>queue</div>;
  },
}));
vi.mock('@/components/Music/RemotePlayerControls', () => ({
  RemotePlayerControls: (props: unknown) => {
    mocks.controls(props);
    return <div>player controls</div>;
  },
}));

describe('ResistanceQueuePanel', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.controls.mockReset();
    mocks.queue.mockReset();
  });

  it('gives a guest the song playing and the queue, without controls or removal', () => {
    render(<ResistanceQueuePanel sessionId="session-1" />);

    expect(screen.getByText('now playing line')).toBeVisible();
    expect(screen.getByText('queue')).toBeVisible();
    expect(screen.queryByText('player controls')).not.toBeInTheDocument();
    expect(mocks.queue).toHaveBeenCalledWith({ sessionId: 'session-1', canRemove: false });
  });

  it('gives an admin the controls and removal, but no volume', () => {
    mocks.role = { isHost: false, isAdmin: true };
    render(<ResistanceQueuePanel sessionId="session-1" />);

    expect(screen.getByText('player controls')).toBeVisible();
    expect(mocks.controls).toHaveBeenCalledWith(
      expect.objectContaining({ isAdmin: true, volume: undefined, onVolumeChange: undefined }),
    );
    expect(mocks.queue).toHaveBeenCalledWith({ sessionId: 'session-1', canRemove: true });
  });

  it('gives the host the volume, from the value kept in the browser', () => {
    mocks.role = { isHost: true, isAdmin: false };
    localStorage.setItem('partyking.volume', '0.7');
    render(<ResistanceQueuePanel sessionId="session-1" />);

    expect(mocks.controls).toHaveBeenCalledWith(
      expect.objectContaining({ isAdmin: false, volume: 0.7, onVolumeChange: expect.any(Function) }),
    );
  });
});
