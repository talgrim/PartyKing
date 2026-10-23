import { ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import useSWR from 'swr';
import { AdminPlayerControls } from '@/components/Music/AdminPlayerControls';
import { HostPlayer } from '@/components/Music/HostPlayer';
import { NowPlayingCard } from '@/components/Music/NowPlayingCard';
import { Queue } from '@/components/Music/Queue';
import { SessionHeader } from '@/components/Music/SessionHeader';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ROUTE_PATHS } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { useNowPlaying } from '@/service/endpoints/player/useNowPlaying';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';

/**
 * Page of a session. Anyone sees what is playing and can search for or paste songs to add to the queue, no login
 * needed. The host also gets the player, and an admin the controls to pause and skip.
 */
export const MusicQueue = (): ReactNode => {
  const { sessionId = '' } = useParams();

  const { isHost, isAdmin } = useViewerRole(sessionId);
  const { data: nowPlaying, mutate: refreshNowPlaying } = useNowPlaying(sessionId);

  const { data: session, error } = useSWR(['session', sessionId], () => sessionsApi.getSession(sessionId), {
    refreshInterval: 10000,
    shouldRetryOnError: false,
  });

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="max-w-xl mx-auto">
        <PageNavigation
          className="px-2 pt-4"
          links={[
            { to: ROUTE_PATHS.HOME, label: 'All sessions' },
            { to: ROUTE_PATHS.SLIDESHOW, label: 'Slideshow' },
            { to: ROUTE_PATHS.IMAGE_UPLOADER, label: 'Upload photos' },
          ]}
        />

        {error && (
          <p className="px-2 pt-4 text-amber-500">
            {(error as { status?: number }).status === HTTP_STATUS_CODE.NOT_FOUND
              ? 'This session does not exist (anymore).'
              : 'Could not load the session.'}{' '}
            <Link to={ROUTE_PATHS.HOME} className="underline">
              Back to the sessions
            </Link>
          </p>
        )}

        {session && (
          <>
            <SessionHeader session={session} isHost={isHost} />
            <NowPlayingCard nowPlaying={nowPlaying} />
            {/* An admin who is also the host plays the music themselves, so only the host's player is shown. */}
            {isHost && <HostPlayer sessionId={session.id} isAdmin={isAdmin} />}
            {!isHost && isAdmin && (
              <AdminPlayerControls
                sessionId={session.id}
                nowPlaying={nowPlaying}
                onChanged={() => void refreshNowPlaying()}
              />
            )}
            <h2 className="px-2 pt-4 text-xl text-slate-200">Add a song to the queue</h2>
            <Queue sessionId={session.id} canRemove={isHost || isAdmin} />
          </>
        )}
      </div>
    </div>
  );
};
