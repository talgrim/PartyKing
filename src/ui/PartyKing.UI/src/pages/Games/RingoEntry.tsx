import { ReactNode, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { sessionRingoPath } from '@/router/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useRingoGame } from '@/service/endpoints/games/useRingoGame';
import { useTranslation } from 'react-i18next';

const buttonClass = 'inline-block mt-2 px-2 py-1 bg-slate-800 rounded-md cursor-pointer disabled:opacity-50 disabled:cursor-default';

/**
 * "Resume" when the session has a Ringo game, "Start new" otherwise. Admins open the game for guests from the board.
 */
export const RingoEntry = ({ sessionId }: { sessionId: string }): ReactNode => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data: game, isLoading, mutate } = useRingoGame(sessionId);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>();

  if (isLoading) return null;

  const start = async () => {
    setBusy(true);
    setMessage(undefined);
    try {
      await mutate(gamesApi.startRingo(sessionId), { revalidate: false });
      navigate(sessionRingoPath(sessionId));
    } catch {
      setMessage(t('ringo.startFailed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      {game ? (
        <Link to={sessionRingoPath(sessionId)} className={buttonClass}>
          {t('ringo.resume')}
        </Link>
      ) : (
        <button type="button" onClick={() => void start()} disabled={busy} className={buttonClass}>
          {t('ringo.startNew')}
        </button>
      )}
      {game?.isOpen && <span className="mt-2 text-slate-400">{t('ringo.openForGuests')}</span>}
      {message && <p className="w-full pt-1 text-amber-500">{message}</p>}
    </div>
  );
};
