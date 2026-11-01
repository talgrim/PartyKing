import { ReactNode, useState } from 'react';
import { mutate as mutateGlobal } from 'swr';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from '@mui/material';
import { SessionPasswordForm } from '@/components/Music/SessionPasswordForm';
import { sessionGamesPath, sessionPath, sessionRingoJoinPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { RingoGameDto } from '@/service/endpoints/games/types';
import { useRingoGame } from '@/service/endpoints/games/useRingoGame';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import { ringoLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { RingoBoard } from './RingoBoard';
import { RingoCardOverlay } from './RingoCardOverlay';
import { RingoGameOver } from './RingoGameOver';
import { endingFlip } from './ringoCards';
import { RingoHistory } from './RingoHistory';
import { RingoPiles } from './RingoPiles';
import { RingoPlayers } from './RingoPlayers';
import { RingoSettings } from './RingoSettings';
import { useTranslation } from 'react-i18next';

const navClass = 'px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700';

/**
 * The Ringo board of a session. The host and admins can flip any time, end the game and manage the players; a player
 * who joined by name flips and continues only on their turn. Anyone else is sent to the join page. Without a game the
 * host and admins go back to the games list, where a game is started, and players to the session page.
 */
export const Ringo = (): ReactNode => {
  const { t } = useTranslation();
  const { sessionId = '' } = useParams();
  const navigate = useNavigate();
  const { data: game, error, mutate } = useRingoGame(sessionId);
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const [managingPlayers, setManagingPlayers] = useState(false);
  const [confirmingEnd, setConfirmingEnd] = useState(false);
  const [message, setMessage] = useState<string>();
  // Kept here, not in the overlay, which closes after every card; the server stores no rules
  const [suggestedRules, setSuggestedRules] = useState<string[]>([]);
  const [historyOpen, setHistoryOpen] = usePersistedFlag('partyking.ringoHistoryOpen', false);

  // The server pushes every change of the game (RingoChanged), so other devices stay current without polling
  useSessionMusicHub(sessionId, game !== undefined, undefined, ['ringo']);

  const ending = game && endingFlip(game.flips);

  const status = (error as { status?: number } | undefined)?.status;
  const canManage = isHost || isAdmin;
  const yourTurn = game?.yourPlayerId != null && game.yourPlayerId === game.currentPlayerId;
  const canAct = canManage || yourTurn;
  const onTurn = game?.players.find((player) => player.id === game.currentPlayerId);

  // Not a player (or kicked): the join page says whether the game is open and asks for a name
  if (status === HTTP_STATUS_CODE.FORBIDDEN) {
    return <Navigate to={sessionRingoJoinPath(sessionId)} replace />;
  }
  if (status === HTTP_STATUS_CODE.NOT_FOUND) {
    return <Navigate to={canManage ? sessionGamesPath(sessionId) : sessionPath(sessionId)} replace />;
  }

  const flip = async (cardId: number) => {
    if (!game) return;
    setMessage(undefined);
    // The server fills in who flipped the card and when
    const optimistic: RingoGameDto = {
      ...game,
      flips: [...game.flips, { cardId, flippedAt: new Date().toISOString(), flippedBy: '…' }],
      pendingCardId: cardId,
    };
    try {
      await mutate(gamesApi.flipRingoCard(sessionId, cardId), {
        optimisticData: optimistic,
        rollbackOnError: true,
        revalidate: false,
      });
    } catch {
      setMessage(t('ringo.flipFailed'));
    }
  };

  const continueGame = async () => {
    if (!game) return;
    setMessage(undefined);
    try {
      await mutate(gamesApi.continueRingo(sessionId), {
        optimisticData: { ...game, pendingCardId: null },
        rollbackOnError: true,
        revalidate: false,
      });
    } catch {
      setMessage(t('ringo.continueFailed'));
    }
  };

  const openGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.openRingo(sessionId), { revalidate: false });
      void mutateGlobal(ringoLobbyKey(sessionId));
    } catch {
      setMessage(t('ringo.openFailed'));
    }
  };

  const endGame = async () => {
    try {
      await gamesApi.endRingo(sessionId);
      await mutate(undefined, { revalidate: false });
      navigate(sessionGamesPath(sessionId), { replace: true });
    } catch {
      setConfirmingEnd(false);
      setMessage(t('ringo.endFailed'));
    }
  };

  return (
    <div className="relative w-full h-screen bg-slate-800 text-slate-400">
      <nav className="absolute inset-x-0 top-0 h-[10%] pl-[max(10%,5rem)] pr-[10%] flex items-center gap-2">
        {canManage ? (
          <Link to={sessionGamesPath(sessionId)} className={navClass}>
            {t('ringo.backToGames')}
          </Link>
        ) : (
          <Link to={sessionPath(sessionId)} className={navClass}>
            {t('common.backToSession')}
          </Link>
        )}
        {game && canManage && (
          <button type="button" onClick={() => setConfirmingEnd(true)} className={`${navClass} cursor-pointer`}>
            {t('ringo.endGame')}
          </button>
        )}
        {game && canManage && (
          <button type="button" onClick={() => setManagingPlayers(true)} className={`${navClass} cursor-pointer`}>
            {t('lobby.players')}
          </button>
        )}
        {game && isAdmin && (
          <button
            type="button"
            onClick={() => void openGame()}
            disabled={game.isOpen}
            className={`${navClass} cursor-pointer disabled:opacity-50 disabled:cursor-default`}
          >
            {t('ringo.openGame')}
          </button>
        )}
        {game && canManage && game.isOpen && game.yourPlayerId == null && (
          <Link to={sessionRingoJoinPath(sessionId)} className={navClass}>
            {t('ringo.joinAsPlayer')}
          </Link>
        )}
        {onTurn && (
          <span className="text-amber-400">
            {t('ringo.turn')} <strong>{onTurn.name}</strong>
          </span>
        )}
        {message && <span className="text-amber-500">{message}</span>}
        {game && <RingoPiles flips={game.flips} pendingCardId={game.pendingCardId} />}
      </nav>

      {status === HTTP_STATUS_CODE.UNAUTHORIZED && (
        <SessionPasswordForm sessionId={sessionId} onJoined={() => void mutate()} />
      )}

      {error && status !== HTTP_STATUS_CODE.UNAUTHORIZED && (
        <p className="absolute top-[12%] px-[10%] text-amber-500">{t('ringo.loadFailed')}</p>
      )}

      {game && (
        <div className="absolute inset-[10%] isolate">
          <RingoBoard
            seed={game.seed}
            pendingCardId={game.pendingCardId}
            gameOver={ending !== undefined}
            disabled={!canAct}
            flippedCards={game.flips.map((flip) => flip.cardId)}
            onFlip={(id) => void flip(id)}
          />
        </div>
      )}

      {game && game.players.length > 0 && (
        <RingoPlayers players={game.players} currentPlayerId={game.currentPlayerId} yourPlayerId={game.yourPlayerId} />
      )}

      {game && <RingoHistory flips={game.flips} open={historyOpen} onToggle={() => setHistoryOpen(!historyOpen)} />}

      {game && ending && <RingoGameOver flip={ending} flips={game.flips} onEnd={canManage ? () => void endGame() : undefined} />}

      {game?.pendingCardId != null && !ending && (
        <RingoCardOverlay
          sessionId={sessionId}
          cardId={game.pendingCardId}
          flips={game.flips}
          suggestedRules={suggestedRules}
          onSuggested={(rules) => setSuggestedRules((current) => [...current, ...rules])}
          onContinue={() => void continueGame()}
          canContinue={canAct}
          canSuggest={canManage}
        />
      )}

      {canManage && <RingoSettings sessionId={sessionId} open={managingPlayers} onClose={() => setManagingPlayers(false)} />}

      <Dialog
        open={confirmingEnd}
        onClose={() => setConfirmingEnd(false)}
        aria-labelledby="end-ringo-title"
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        <DialogTitle id="end-ringo-title">{t('ringo.endTitle')}</DialogTitle>
        <DialogContent>{t('ringo.endBody')}</DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmingEnd(false)}>{t('common.cancel')}</Button>
          <Button color="error" onClick={() => void endGame()}>
            {t('ringo.endGame')}
          </Button>
        </DialogActions>
      </Dialog>
    </div>
  );
};
