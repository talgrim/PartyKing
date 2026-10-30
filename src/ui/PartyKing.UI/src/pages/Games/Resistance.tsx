import { ReactNode, useState } from 'react';
import { Navigate, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { mutate as mutateGlobal } from 'swr';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import useMediaQuery from '@mui/material/useMediaQuery';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ResistanceManagerControls } from '@/pages/Games/resistance/ResistanceManagerControls';
import { ResistanceCompactBoard } from '@/pages/Games/resistance/ResistanceCompactBoard';
import { ResistancePlotLog } from './resistance/ResistancePlotLog';
import { ResistancePhaseBanner } from '@/pages/Games/resistance/ResistancePhaseBanner';
import { ResistancePlayerView } from '@/pages/Games/resistance/ResistancePlayerView';
import { ResistanceTvBoard } from '@/pages/Games/resistance/ResistanceTvBoard';
import { ResistanceViewSwitch } from '@/pages/Games/resistance/ResistanceViewSwitch';
import { resistancePhaseDescription } from '@/pages/Games/resistance/resistancePhase';
import { RESISTANCE_RULES } from '@/pages/Games/resistance/resistanceRules';
import { missionBoardFor } from '@/pages/Games/resistance/resistanceTeams';
import {
  sessionGamesPath,
  sessionPath,
  sessionResistanceJoinPath,
  sessionResistancePath,
} from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { withDevPersona } from '@/service/devPersona';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceGame } from '@/service/endpoints/games/useResistanceGame';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import { resistanceLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { ResistanceSettings } from './ResistanceSettings';

/** From this width a player has room for their own view and the board side by side, so there is no switch. */
const WIDE_QUERY = '(min-width: 1024px)';

/**
 * The Resistance page: the mission board for the players who joined so far, the players around the edge and the
 * rules summary behind the "i" button. The host and admins manage the players, an admin opens the game to guests
 * (the 5-player team sizes are shown dimmed until five joined). Guests who unlocked the session watch once the game is
 * open. The host or an admin starts the game with at least five players: that locks the players, shows the first
 * leader's crown and gives every player a face-down card with their secret role (a spy also sees the other spies).
 * Anyone else only watches. The leader picks the team, every player votes on it (a vote can be changed until the
 * last player has voted; the votes become public then), and a rejected team passes the lead on after "Dalej" (the
 * leader, host or an admin). Five rejected teams in a row end the game with a spy win. An approved team plays the mission: every
 * member secretly plays Success or Fail (a Resistance member only Success), only the number of Fail cards becomes
 * public, and "Dalej" starts the next mission. Three successes or failures end the game.
 *
 * Two layouts share the page. The TV layout (`?tv=1` for the host and admins, and for a host or admin who is not
 * playing) has the manager controls, the board in the middle and the players around the edge, and never shows the
 * viewer's role or "Ty", since it is meant for a big screen. Everyone else gets a phone layout: a player switches
 * between their own view (the default, with the role card) and the board with the player list; the choice is kept in
 * `localStorage`; from 1024 px wide a player sees both at once, without the switch. Observers only get the board.
 */
export const Resistance = (): ReactNode => {
  const { sessionId = '' } = useParams();
  const navigate = useNavigate();
  const { data: game, error, mutate } = useResistanceGame(sessionId);
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const [searchParams] = useSearchParams();
  const wide = useMediaQuery(WIDE_QUERY);
  const [boardView, setBoardView] = usePersistedFlag('partyking.resistanceBoardView', false);
  const [rulesOpen, setRulesOpen] = useState(false);
  const [managingPlayers, setManagingPlayers] = useState(false);
  const [confirmingEnd, setConfirmingEnd] = useState(false);
  const [controlsOpen, setControlsOpen] = useState(false);
  const [message, setMessage] = useState<string>();

  // The server pushes every change of the lobby (ResistanceChanged), so other devices stay current without polling
  useSessionMusicHub(sessionId, game !== undefined);

  const canManage = isHost || isAdmin;
  const status = (error as { status?: number } | undefined)?.status;

  // No lobby yet (or not open to guests): the games list for the host and admins, the session page for the rest
  if (status === HTTP_STATUS_CODE.NOT_FOUND) {
    return <Navigate to={canManage ? sessionGamesPath(sessionId) : sessionPath(sessionId)} replace />;
  }

  const tv = canManage && searchParams.get('tv') === '1';
  const isPlayer = game?.yourPlayerId != null;
  const tvLayout = tv || (canManage && !isPlayer);
  const showSwitch = isPlayer && !tv && !wide;
  const showPlayerView = isPlayer && !tv && (wide || !boardView);
  const showBoard = !showPlayerView || wide;
  // The TV tab shares the player's cookies, so it must not show the role or the "Ty" badge of whoever is logged in
  const shownGame = game && tv ? { ...game, yourPlayerId: null, yourRole: null, fellowSpies: null, yourVote: null, yourMissionCard: null, yourPlotCards: [], yourReveals: [], yourPeeks: [] } : game;

  const players = game?.players ?? [];
  const started = game?.startedAt != null;
  const board = missionBoardFor(players.length, game?.rules);

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

  const refreshRules = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.refreshResistanceRules(sessionId), { revalidate: false });
    } catch {
      setMessage('Could not refresh the rules.');
    }
  };

  const setPlotCards = async (enabled: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.setResistancePlotCards(sessionId, enabled), { revalidate: false });
    } catch {
      setMessage('Could not change the plot cards.');
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

  const submitTeam = async (playerIds: string[]) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.selectResistanceTeam(sessionId, playerIds), { revalidate: false });
    } catch {
      setMessage('Could not submit the team.');
    }
  };

  const dealPlotCards = async (assignments: { cardId: number; playerId: string }[]) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.dealResistancePlotCards(sessionId, assignments), { revalidate: false });
    } catch {
      setMessage('Could not deal the cards.');
    }
  };

  const playPlotCard = async (cardId: number, targetPlayerId: string) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.playResistancePlotCard(sessionId, cardId, targetPlayerId), { revalidate: false });
    } catch {
      setMessage('Could not play the card.');
    }
  };

  const takeResponsibility = async (cardId: number, targetPlayerId: string, takeOpinionMaker: boolean | null) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.takeResistanceResponsibility(sessionId, cardId, targetPlayerId, takeOpinionMaker), { revalidate: false });
    } catch {
      setMessage('Could not take the card.');
    }
  };

  const takeLead = async (cardId: number) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.takeResistanceLead(sessionId, cardId), { revalidate: false });
    } catch {
      setMessage('Could not take the lead.');
    }
  };

  const peekMissionCard = async (cardId: number, targetPlayerId: string) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.peekResistanceMissionCard(sessionId, cardId, targetPlayerId), { revalidate: false });
    } catch {
      setMessage('Could not look at the card.');
    }
  };

  const playNoConfidence = async (cardId: number) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.playResistanceNoConfidence(sessionId, cardId), { revalidate: false });
    } catch {
      setMessage('Could not play the card.');
    }
  };

  const passLeaderWindow = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.passResistanceLeaderWindow(sessionId), { revalidate: false });
    } catch {
      setMessage('Could not confirm.');
    }
  };

  const confirmPlotCard = async (cardId: number) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.confirmResistancePlotCard(sessionId, cardId), { revalidate: false });
    } catch {
      setMessage('Could not confirm the card.');
    }
  };

  const castVote = async (approve: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.voteResistanceTeam(sessionId, approve), { revalidate: false });
    } catch {
      setMessage('Could not send the vote.');
    }
  };

  const playMissionCard = async (success: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.playResistanceMissionCard(sessionId, success), { revalidate: false });
    } catch {
      setMessage('Could not play the mission card.');
    }
  };

  const continueGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.continueResistance(sessionId), { revalidate: false });
    } catch {
      setMessage('Could not move on.');
    }
  };

  const restartGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.restartResistance(sessionId), { revalidate: false });
    } catch {
      setMessage('Could not restart the game.');
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

  const banner = shownGame && (
    <>
      <ResistancePhaseBanner text={resistancePhaseDescription(shownGame, { canManage, isAdmin }, board.teamSizes[0])} />
      <ResistancePlotLog cards={shownGame.playedPlotCards} players={shownGame.players} />
    </>
  );

  const rulesButton = (
    <button
      type="button"
      onClick={() => setRulesOpen(true)}
      aria-label="Rules"
      title="Rules"
      className="ml-auto p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
    >
      <InfoOutlinedIcon />
    </button>
  );

  const managerControls = game && canManage && (
    <ResistanceManagerControls
      game={game}
      isAdmin={isAdmin}
      onManagePlayers={() => setManagingPlayers(true)}
      onEnd={() => setConfirmingEnd(true)}
      onStart={() => void startGame()}
      onOpen={() => void openGame()}
      onRefreshRules={() => void refreshRules()}
      onSetPlotCards={(enabled) => void setPlotCards(enabled)}
      onContinue={() => void continueGame()}
      onRestart={() => void restartGame()}
    />
  );

  const playerCount = game && (
    <span className="text-slate-400">
      {players.length}/{game.maxPlayers} graczy
    </span>
  );

  return (
    <div
      className={
        tvLayout
          ? 'relative w-full h-screen bg-slate-800 text-slate-400'
          : 'flex flex-col gap-3 w-full min-h-screen p-3 bg-slate-800 text-slate-400'
      }
    >
      {tvLayout ? (
        <nav className="absolute inset-x-0 top-0 h-[10%] px-[10%] flex items-center">
          <div className="flex items-center gap-2 w-full">
            <PageNavigation
              links={[
                back,
                ...(game && canManage && game.isOpen && !started && game.yourPlayerId == null
                  ? [{ to: sessionResistanceJoinPath(sessionId), label: 'Join as a player' }]
                  : []),
              ]}
            />
            {managerControls}
            {playerCount}
            {message && <span className="text-amber-500">{message}</span>}
            {rulesButton}
          </div>
        </nav>
      ) : (
        <nav className="flex flex-wrap items-center gap-2">
          <PageNavigation links={[back]} />
          {showSwitch && <ResistanceViewSwitch showBoard={boardView} onChange={setBoardView} />}
          {playerCount}
          {message && <span className="text-amber-500">{message}</span>}
          {canManage && (
            <button
              type="button"
              onClick={() => setControlsOpen((open) => !open)}
              aria-expanded={controlsOpen}
              aria-controls="resistance-manager-controls"
              className="px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700 cursor-pointer"
            >
              Controls
            </button>
          )}
          {rulesButton}
        </nav>
      )}

      {!tvLayout && canManage && controlsOpen && (
        <div id="resistance-manager-controls" role="group" aria-label="Game controls" className="flex flex-wrap gap-2">
          <a
            href={withDevPersona(`${sessionResistancePath(sessionId)}?tv=1`)}
            target="_blank"
            rel="noopener noreferrer"
            className="px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700"
          >
            Open TV view
          </a>
          {managerControls}
        </div>
      )}

      {tvLayout ? (
        <ResistanceTvBoard game={game} board={board} banner={banner} yourPlayerId={shownGame?.yourPlayerId ?? null} />
      ) : (
        <>
          {banner}
          <div className={wide ? 'flex items-start gap-6' : undefined}>
            {showPlayerView && game && (
              <div className={wide ? 'w-1/3 shrink-0' : undefined}>
                <ResistancePlayerView
                  game={game}
                  onSubmitTeam={submitTeam}
                  onDealPlotCards={dealPlotCards}
                  onPlayPlotCard={playPlotCard}
                  onTakeResponsibility={takeResponsibility}
                  onTakeLead={takeLead}
                  onNoConfidence={playNoConfidence}
                  onPeekMissionCard={peekMissionCard}
                  onPassLeaderWindow={passLeaderWindow}
                  onConfirmPlotCard={confirmPlotCard}
                  onVote={castVote}
                  onPlayMissionCard={playMissionCard}
                  onContinue={continueGame}
                />
              </div>
            )}
            {showBoard && (
              <div className={wide ? 'flex-1 min-w-0' : undefined}>
                <ResistanceCompactBoard game={game} board={board} />
              </div>
            )}
          </div>
        </>
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
