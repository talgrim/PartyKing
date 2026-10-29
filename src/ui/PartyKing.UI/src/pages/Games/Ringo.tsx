import { ReactNode, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from '@mui/material';
import { SessionPasswordForm } from '@/components/Music/SessionPasswordForm';
import { sessionGamesPath, sessionPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { RingoGameDto } from '@/service/endpoints/games/types';
import { useRingoGame } from '@/service/endpoints/games/useRingoGame';
import { RingoBoard } from './RingoBoard';

const navClass = 'px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700';

/**
 * The Ringo board of a session, for the host and admins (the server refuses guests, who are sent back to the session
 * page). Without a game it goes back to the games list, where a game is started.
 */
export const Ringo = (): ReactNode => {
  const { sessionId = '' } = useParams();
  const navigate = useNavigate();
  const { data: game, error, mutate } = useRingoGame(sessionId);
  const [confirmingEnd, setConfirmingEnd] = useState(false);
  const [message, setMessage] = useState<string>();

  const status = (error as { status?: number } | undefined)?.status;

  if (status === HTTP_STATUS_CODE.FORBIDDEN) {
    return <Navigate to={sessionPath(sessionId)} replace />;
  }
  if (status === HTTP_STATUS_CODE.NOT_FOUND) {
    return <Navigate to={sessionGamesPath(sessionId)} replace />;
  }

  const flip = async (cardId: number) => {
    if (!game) return;
    setMessage(undefined);
    const optimistic: RingoGameDto = { ...game, flippedCards: [...game.flippedCards, cardId] };
    try {
      await mutate(gamesApi.flipRingoCard(sessionId, cardId), {
        optimisticData: optimistic,
        rollbackOnError: true,
        revalidate: false,
      });
    } catch {
      setMessage('Could not flip the card.');
    }
  };

  const endGame = async () => {
    try {
      await gamesApi.endRingo(sessionId);
      await mutate(undefined, { revalidate: false });
      navigate(sessionGamesPath(sessionId), { replace: true });
    } catch {
      setConfirmingEnd(false);
      setMessage('Could not end the game.');
    }
  };

  return (
    <div className="relative w-full h-screen bg-slate-800 text-slate-400">
      <nav className="absolute inset-x-0 top-0 h-[10%] px-[10%] flex items-center gap-2">
        <Link to={sessionGamesPath(sessionId)} className={navClass}>
          Back to the games
        </Link>
        {game && (
          <button type="button" onClick={() => setConfirmingEnd(true)} className={`${navClass} cursor-pointer`}>
            End game
          </button>
        )}
        {message && <span className="text-amber-500">{message}</span>}
      </nav>

      {status === HTTP_STATUS_CODE.UNAUTHORIZED && (
        <SessionPasswordForm sessionId={sessionId} onJoined={() => void mutate()} />
      )}

      {error && status !== HTTP_STATUS_CODE.UNAUTHORIZED && (
        <p className="absolute top-[12%] px-[10%] text-amber-500">Could not load the game.</p>
      )}

      {game && (
        <div className="absolute inset-[10%]">
          <RingoBoard seed={game.seed} flippedCards={game.flippedCards} onFlip={(id) => void flip(id)} />
        </div>
      )}

      <Dialog
        open={confirmingEnd}
        onClose={() => setConfirmingEnd(false)}
        aria-labelledby="end-ringo-title"
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        <DialogTitle id="end-ringo-title">End the game?</DialogTitle>
        <DialogContent>The cards are put away and the game can't be resumed. A new one can be started afterwards.</DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmingEnd(false)}>Cancel</Button>
          <Button color="error" onClick={() => void endGame()}>
            End game
          </Button>
        </DialogActions>
      </Dialog>
    </div>
  );
};
