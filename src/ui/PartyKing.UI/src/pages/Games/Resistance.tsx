import { ReactNode, useState } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
import { mutate as mutateGlobal } from 'swr';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { PageNavigation } from '@/components/common/PageNavigation';
import { MissionBoardIcon } from '@/pages/Games/resistance/ResistanceIcons';
import { RESISTANCE_RULES } from '@/pages/Games/resistance/resistanceRules';
import { MIN_PLAYERS, missionBoardFor } from '@/pages/Games/resistance/resistanceTeams';
import { sessionGamesPath, sessionPath, sessionResistanceJoinPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceGame } from '@/service/endpoints/games/useResistanceGame';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import { resistanceLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import { ResistancePlayers } from './ResistancePlayers';
import { ResistanceRoleCard } from './ResistanceRoleCard';
import { ResistanceSettings } from './ResistanceSettings';

const NO_RESULTS = [null, null, null, null, null] as const;

const navClass = 'px-3 py-2 text-slate-200 bg-slate-900 rounded-md';

/**
 * The Resistance page: the mission board for the players who joined so far, the players around the edge and the
 * rules summary behind the "i" button. The host and admins manage the players, an admin opens the game to guests
 * (the 5-player team sizes are shown dimmed until five joined). Guests who unlocked the session watch once the game is
 * open. The host or an admin starts the game with at least five players: that locks the players, shows the first
 * leader's crown and gives every player a face-down card with their secret role (a spy also sees the other spies).
 * Anyone else only watches. Nothing more is played yet.
 */
export const Resistance = (): ReactNode => {
  const { sessionId = '' } = useParams();
  const navigate = useNavigate();
  const { data: game, error, mutate } = useResistanceGame(sessionId);
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const [rulesOpen, setRulesOpen] = useState(false);
  const [managingPlayers, setManagingPlayers] = useState(false);
  const [confirmingEnd, setConfirmingEnd] = useState(false);
  const [message, setMessage] = useState<string>();

  // The server pushes every change of the lobby (ResistanceChanged), so other devices stay current without polling
  useSessionMusicHub(sessionId, game !== undefined);

  const canManage = isHost || isAdmin;
  const status = (error as { status?: number } | undefined)?.status;

  // No lobby yet (or not open to guests): the games list for the host and admins, the session page for the rest
  if (status === HTTP_STATUS_CODE.NOT_FOUND) {
    return <Navigate to={canManage ? sessionGamesPath(sessionId) : sessionPath(sessionId)} replace />;
  }

  const players = game?.players ?? [];
  const started = game?.startedAt != null;
  const board = missionBoardFor(players.length);

  const back = canManage
    ? { to: sessionGamesPath(sessionId), label: 'Back to the games' }
    : { to: sessionPath(sessionId), label: 'Back to the session' };

  const openGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.openResistance(sessionId), { revalidate: false });
      void mutateGlobal(resistanceLobbyKey(sessionId));
    } catch {
      setMessage('Could not open the game.');
    }
  };

  const startGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.startResistance(sessionId), { revalidate: false });
      void mutateGlobal(resistanceLobbyKey(sessionId));
    } catch {
      setMessage('Could not start the game.');
    }
  };

  const endGame = async () => {
    try {
      await gamesApi.endResistance(sessionId);
      await mutate(undefined, { revalidate: false });
      void mutateGlobal(resistanceLobbyKey(sessionId));
      navigate(sessionGamesPath(sessionId), { replace: true });
    } catch {
      setConfirmingEnd(false);
      setMessage('Could not end the game.');
    }
  };

  return (
    <div className="relative w-full h-screen bg-slate-800 text-slate-400">
      <nav className="absolute inset-x-0 top-0 h-[10%] px-[10%] flex items-center">
        <div className="flex items-center gap-2 w-full">
          <PageNavigation
            links={[
              back,
              ...(game && canManage && game.isOpen && !started && game.yourPlayerId == null
                ? [{ to: sessionResistanceJoinPath(sessionId), label: 'Join as a player' }]
                : []),
              ...(game && canManage && !started ? [{ onClick: () => setManagingPlayers(true), label: 'Players' }] : []),
              ...(game && canManage ? [{ onClick: () => setConfirmingEnd(true), label: 'End game' }] : []),
            ]}
          />
          {game && canManage && game.isOpen && !started && (
            <button
              type="button"
              onClick={() => void startGame()}
              disabled={players.length < MIN_PLAYERS}
              title={players.length < MIN_PLAYERS ? `Potrzeba co najmniej ${MIN_PLAYERS} graczy` : undefined}
              className={`${navClass} cursor-pointer disabled:opacity-50 disabled:cursor-default`}
            >
              Start game
            </button>
          )}
          {game && isAdmin && !started && (
            <button
              type="button"
              onClick={() => void openGame()}
              disabled={game.isOpen}
              className={`${navClass} cursor-pointer disabled:opacity-50 disabled:cursor-default`}
            >
              Open the game
            </button>
          )}
          {game && (
            <span className="text-slate-400">
              {players.length}/{game.maxPlayers} graczy
            </span>
          )}
          {message && <span className="text-amber-500">{message}</span>}
          <button
            type="button"
            onClick={() => setRulesOpen(true)}
            aria-label="Rules"
            title="Rules"
            className="ml-auto p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
          >
            <InfoOutlinedIcon />
          </button>
        </div>
      </nav>

      {/* The board keeps to the middle 80%; the players sit in the margins around it */}
      <div className="absolute inset-[10%] flex flex-col items-center justify-center gap-4 overflow-hidden">
        {game?.yourRole != null && (
          <ResistanceRoleCard role={game.yourRole} players={players} fellowSpyIds={game.fellowSpies ?? []} />
        )}

        {board.provisional && game && !started && (
          <p className="text-sm text-slate-400">
            Potrzeba co najmniej 5 graczy. Pokazane rozmiary drużyn dotyczą gry w 5 osób.
          </p>
        )}

        <MissionBoardIcon
          className="w-full max-h-full"
          title="Mission board"
          teamSizes={board.teamSizes}
          results={NO_RESULTS}
          currentMission={null}
          rejectedTeams={0}
          fourthMissionNeedsTwoFails={board.fourthMissionNeedsTwoFails}
          dimmed={board.provisional}
        />
      </div>

      {game && players.length > 0 && (
        <ResistancePlayers players={players} yourPlayerId={game.yourPlayerId} leaderId={game.leaderPlayerId} />
      )}

      {canManage && !started && (
        <ResistanceSettings sessionId={sessionId} open={managingPlayers} onClose={() => setManagingPlayers(false)} />
      )}

      <Dialog
        open={confirmingEnd}
        onClose={() => setConfirmingEnd(false)}
        aria-labelledby="end-resistance-title"
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        <DialogTitle id="end-resistance-title">End the game?</DialogTitle>
        <DialogContent>The players are sent away and the game can't be resumed. A new one can be created afterwards.</DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmingEnd(false)}>Cancel</Button>
          <Button color="error" onClick={() => void endGame()}>
            End game
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={rulesOpen}
        onClose={() => setRulesOpen(false)}
        aria-labelledby="resistance-rules-title"
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        <DialogTitle id="resistance-rules-title">Zasady gry Resistance</DialogTitle>
        <DialogContent className="space-y-3">
          {RESISTANCE_RULES.map((rule) => (
            <section key={rule.title}>
              <h2 className="text-slate-200">{rule.title}</h2>
              <p className="text-sm text-slate-400">{rule.text}</p>
            </section>
          ))}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRulesOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </div>
  );
};
