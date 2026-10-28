import { ReactNode } from 'react';
import { renderHook, waitFor } from '@testing-library/react';
import { HubConnection } from '@microsoft/signalr';
import { SessionHubContext } from '@/service/realtime/SessionHubContext';
import { SessionHubHandle } from '@/service/realtime/useSessionMusicHub';
import { useSlideshowWatch } from '@/service/realtime/useSlideshowWatch';

const fakeConnection = () => ({ invoke: vi.fn().mockResolvedValue(undefined) });

const renderWatch = (handle: SessionHubHandle) =>
  renderHook(() => useSlideshowWatch(), {
    wrapper: ({ children }: { children: ReactNode }) => (
      <SessionHubContext.Provider value={handle}>{children}</SessionHubContext.Provider>
    ),
  });

describe('useSlideshowWatch', () => {
  it('tells the server the slideshow is displayed once the hub is connected', () => {
    const connection = fakeConnection();

    const { result } = renderWatch({ connection: connection as unknown as HubConnection, connected: true });

    expect(connection.invoke).toHaveBeenCalledWith('WatchSlideshow');
    expect(result.current).toBe(true);
  });

  it('tells the server it stopped when the view unmounts', () => {
    const connection = fakeConnection();
    const { unmount } = renderWatch({ connection: connection as unknown as HubConnection, connected: true });

    unmount();

    expect(connection.invoke).toHaveBeenLastCalledWith('StopWatchingSlideshow');
  });

  it('does nothing while the hub is not connected', () => {
    const connection = fakeConnection();

    const { result } = renderWatch({ connection: connection as unknown as HubConnection, connected: false });

    expect(connection.invoke).not.toHaveBeenCalled();
    expect(result.current).toBe(false);
  });

  it('survives the server refusing it', async () => {
    const connection = { invoke: vi.fn().mockRejectedValue(new Error('Forbidden')) };

    const { result } = renderWatch({ connection: connection as unknown as HubConnection, connected: true });

    await waitFor(() => expect(connection.invoke).toHaveBeenCalled());
    expect(result.current).toBe(true);
  });
});
