import { ReactNode, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { sessionResistancePath } from '@/router/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceGame } from '@/service/endpoints/games/useResistanceGame';

const buttonClass = 'inline-block mt-2 px-2 py-1 bg-slate-800 rounded-md cursor-pointer disabled:opacity-50 disabled:cursor-default';

/**
 * "Resume" when the session has a Resistance lobby, "Create" otherwise. Admins open the lobby for guests from the board.
 */
export const ResistanceEntry = ({ sessionId }: { sessionId: string }): ReactNode => {
  const navigate = useNavigate();
  const { data: game, isLoading, mutate } = useResistanceGame(sessionId);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>();

  if (isLoading) return null;

  const create = async () => {
    setBusy(true);
    setMessage(undefined);
    try {
      await mutate(gamesApi.createResistance(sessionId), { revalidate: false });
      navigate(sessionResistancePath(sessionId));
    } catch {
      setMessage('Could not create the game.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      {game ? (
        <Link to={sessionResistancePath(sessionId)} className={buttonClass}>
          Resume
        </Link>
      ) : (
        <button type="button" onClick={() => void create()} disabled={busy} className={buttonClass}>
          Create
        </button>
      )}
      {game?.isOpen && <span className="mt-2 text-slate-400">Open for guests</span>}
      {message && <p className="w-full pt-1 text-amber-500">{message}</p>}
    </div>
  );
};
