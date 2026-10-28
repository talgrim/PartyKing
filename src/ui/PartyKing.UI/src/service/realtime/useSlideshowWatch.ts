import { useEffect } from 'react';
import { SESSION_HUB_METHODS } from '@/service/realtime/sessionMusicEvents';
import { useSessionHub } from '@/service/realtime/SessionHubContext';

/**
 * While the calling view is mounted and the hub is connected, tells the server this connection displays the
 * slideshow, so its timer runs and pushes the slide changes. Watching starts again after every reconnect, because the
 * server forgets it with the old connection. Returns whether the push channel is up (the view polls fast without it).
 */
export const useSlideshowWatch = (): boolean => {
  const { connection, connected } = useSessionHub();

  useEffect(() => {
    if (!connection || !connected) return;

    // A refusal (not host or admin) or a dropped call leaves the slideshow on the slow poll, so there is nothing to do
    void connection.invoke(SESSION_HUB_METHODS.WatchSlideshow).catch(() => undefined);

    return () => {
      void connection.invoke(SESSION_HUB_METHODS.StopWatchingSlideshow).catch(() => undefined);
    };
  }, [connection, connected]);

  return connected;
};
