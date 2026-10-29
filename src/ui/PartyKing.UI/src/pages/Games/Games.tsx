import { ReactNode } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { useSWRConfig } from 'swr';
import { NeverHaveIEverSuggestions } from '@/pages/Games/NeverHaveIEverSuggestions';
import { RingoEntry } from '@/pages/Games/RingoEntry';
import { SessionPasswordForm } from '@/components/Music/SessionPasswordForm';
import { PageNavigation } from '@/components/common/PageNavigation';
import { sessionPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { useGames } from '@/service/endpoints/games/useGames';

const NEVER_HAVE_I_EVER_ID = 'never-have-i-ever';
const RINGO_ID = 'ringo';

/**
 * Games of a session. Only the host and admins may open it: the server refuses guests, who are sent back to the
 * session page. A locked session asks for its password first.
 */
export const Games = (): ReactNode => {
  const { sessionId = '' } = useParams();
  const { mutate } = useSWRConfig();
  const { data: games, error } = useGames(sessionId);

  const status = (error as { status?: number } | undefined)?.status;

  if (status === HTTP_STATUS_CODE.FORBIDDEN) {
    return <Navigate to={sessionPath(sessionId)} replace />;
  }

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="w-full max-w-xl mx-auto">
        <PageNavigation className="px-2 pt-4" links={[{ to: sessionPath(sessionId), label: 'Back to the session' }]} />
        <h1 className="px-2 pt-4 text-2xl text-slate-200">Games</h1>

        {status === HTTP_STATUS_CODE.UNAUTHORIZED && (
          <SessionPasswordForm sessionId={sessionId} onJoined={() => void mutate(() => true)} />
        )}

        {error && status !== HTTP_STATUS_CODE.UNAUTHORIZED && (
          <p className="px-2 pt-4 text-amber-500">
            {status === HTTP_STATUS_CODE.NOT_FOUND ? 'This session does not exist (anymore).' : 'Could not load the games.'}{' '}
            <Link to="/" className="underline">
              Back to the sessions
            </Link>
          </p>
        )}

        {games && (
          <ul className="px-2 py-4 space-y-2">
            {games.map((game) => (
              <li key={game.id} className="p-3 bg-slate-900 rounded-md">
                <div className="text-lg text-slate-200">{game.name}</div>
                <div className="text-sm text-slate-500">{game.description}</div>
                {game.id === RINGO_ID && <RingoEntry sessionId={sessionId} />}
                {game.id === NEVER_HAVE_I_EVER_ID && <NeverHaveIEverSuggestions sessionId={sessionId} />}
              </li>
            ))}
            {games.length === 0 && <li>There are no games yet.</li>}
          </ul>
        )}
      </div>
    </div>
  );
};
