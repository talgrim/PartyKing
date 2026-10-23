import { ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import useSWR from 'swr';
import { Queue } from '@/components/Music/Queue';
import { SessionHeader } from '@/components/Music/SessionHeader';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ROUTE_PATHS } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';

/** Guest page of a session: anyone can search for or paste songs and add them to its queue. No login needed. */
export const MusicQueue = (): ReactNode => {
  const { sessionId = '' } = useParams();

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
            <SessionHeader session={session} />
            <h2 className="px-2 pt-4 text-xl text-slate-200">Add a song to the queue</h2>
            <Queue sessionId={session.id} />
          </>
        )}
      </div>
    </div>
  );
};
