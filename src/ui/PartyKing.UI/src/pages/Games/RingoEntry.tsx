import { ReactNode, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { sessionRingoPath } from '@/router/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useRingoGame } from '@/service/endpoints/games/useRingoGame';

const buttonClass = 'inline-block mt-2 px-2 py-1 bg-slate-800 rounded-md cursor-pointer disabled:opacity-50';

/** "Resume" when the session has a Ringo game, "Start new" otherwise. */
export const RingoEntry = ({ sessionId }: { sessionId: string }): ReactNode => {
  const navigate = useNavigate();
  const { data: game, isLoading, mutate } = useRingoGame(sessionId);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>();

  if (isLoading) return null;

  if (game) {
    return (
      <Link to={sessionRingoPath(sessionId)} className={buttonClass}>
        Resume
      </Link>
    );
  }

  const start = async () => {
    setBusy(true);
    setMessage(undefined);
    try {
      await mutate(gamesApi.startRingo(sessionId), { revalidate: false });
      navigate(sessionRingoPath(sessionId));
    } catch {
      setMessage('Could not start the game.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <button type="button" onClick={() => void start()} disabled={busy} className={buttonClass}>
        Start new
      </button>
      {message && <p className="pt-1 text-amber-500">{message}</p>}
    </div>
  );
};
