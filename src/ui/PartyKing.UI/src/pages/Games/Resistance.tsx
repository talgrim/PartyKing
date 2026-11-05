import { ReactNode, useMemo, useState } from 'react';
import { Link, Navigate, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { mutate as mutateGlobal } from 'swr';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import useMediaQuery from '@mui/material/useMediaQuery';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import SettingsIcon from '@mui/icons-material/Settings';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ResistanceManagerControls } from '@/pages/Games/resistance/ResistanceManagerControls';
import { ResistanceCompactBoard } from '@/pages/Games/resistance/ResistanceCompactBoard';
import { ResistancePlotLog } from './resistance/ResistancePlotLog';
import { ResistancePhaseBanner } from '@/pages/Games/resistance/ResistancePhaseBanner';
import { ResistancePlayerView } from '@/pages/Games/resistance/ResistancePlayerView';
import { ResistanceTvBoard } from '@/pages/Games/resistance/ResistanceTvBoard';
import { ResistanceQueuePanel } from '@/pages/Games/resistance/ResistanceQueuePanel';
import { ResistanceViewSwitch } from '@/pages/Games/resistance/ResistanceViewSwitch';
import { ResistanceView, useResistanceView } from '@/pages/Games/resistance/useResistanceView';
import { resistancePhaseDescription } from '@/pages/Games/resistance/resistancePhase';
import { resistanceRules } from '@/pages/Games/resistance/resistanceRules';
import { missionBoardFor } from '@/pages/Games/resistance/resistanceTeams';
import {
  devSessionResistancePanelsPath,
  sessionGamesPath,
  sessionPath,
  sessionResistanceIconsPath,
  sessionResistanceJoinPath,
  sessionResistancePath,
} from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { isDevEmbedded, withDevPersona } from '@/service/devPersona';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceGame } from '@/service/endpoints/games/useResistanceGame';
import { useResistancePresence } from '@/service/endpoints/games/useResistancePresence';
import { ResistanceMissionCardKind } from '@/service/endpoints/games/types';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import {
  RESISTANCE_HUB_TOPICS,
  RESISTANCE_TV_HUB_TOPICS,
  resistanceLobbyKey,
  withMusicTopic,
} from '@/service/realtime/sessionMusicEvents';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { ResistanceGameSettings } from './ResistanceGameSettings';
import { ResistanceSeatsDialog } from '@/pages/Games/resistance/ResistanceSeatsDialog';
import { ResistanceSettings } from './ResistanceSettings';
import { useTranslation } from 'react-i18next';

/**
 * From this width a player has room for their own view next to the board, so the switch only chooses what the right
 * column shows: the board or the music queue.
 */
const WIDE_QUERY = '(min-width: 1024px)';
const WIDE_VIEWS: readonly ResistanceView[] = ['board', 'queue'];

/**
 * The Resistance page: the mission board for the players who joined so far, the players around the edge and the
 * rules summary behind the "i" button. The host and admins manage the players, an admin opens the game to guests
 * (the 5-player team sizes are shown dimmed until five joined). Guests who unlocked the session watch once the game is
 * open. The host or an admin starts the game with at least five players: that locks the players, shows the first
 * leader's crown and gives every player a face-down card with their secret role (a spy also sees the other spies).
 * Anyone else only watches. The leader picks the team, every player votes on it (a vote can be changed until the
 * last player has voted; the votes become public then), and a rejected team passes the lead on after "Next" (the
 * leader, host or an admin). Five rejected teams in a row end the game with a spy win. An approved team plays the mission: every
 * member secretly plays Success or Fail (a Resistance member only Success), only the number of Fail cards becomes
 * public, and "Next" starts the next mission. Three successes or failures end the game.
 *
 * Two layouts share the page. The TV layout (`?tv=1` for the host and admins, and for a host or admin who is not
 * playing) has the manager controls, the board in the middle and the players around the edge, and never shows the
 * viewer's role or "Ty", since it is meant for a big screen. Everyone else gets a phone layout: a player switches
 * between their own view (the default, with the role card), the board with the player list and the music queue; the
 * choice is kept in `localStorage`; from 1024 px wide a player always sees their own view and the switch picks the
 * board or the queue next to it. The host and admins who are not playing get a "Queue" button in the TV navigation
 * that opens the queue in a dialog. Observers only get the board.
 */
export const Resistance = (): ReactNode => {
  const { t } = useTranslation();
  const { sessionId = '' } = useParams();
  const navigate = useNavigate();
  const { data: game, error, mutate } = useResistanceGame(sessionId);
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const [searchParams] = useSearchParams();
  // A frame of the multi-player dev page (development only) shows nothing but the player's own view, however wide it is
  const embedded = isDevEmbedded;
  const wide = useMediaQuery(WIDE_QUERY) && !embedded;
  const [view, setView] = useResistanceView();
  const [logHidden, setLogHidden] = usePersistedFlag('partyking.resistanceLogHidden', false);
  const [rulesOpen, setRulesOpen] = useState(false);
  const [managingPlayers, setManagingPlayers] = useState(false);
  const [seatsOpen, setSeatsOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [queueOpen, setQueueOpen] = useState(false);
  const [wasPlayer, setWasPlayer] = useState(false);
  const [confirmingEnd, setConfirmingEnd] = useState(false);
  const [controlsOpen, setControlsOpen] = useState(false);
  const [message, setMessage] = useState<string>();

  const canManage = isHost || isAdmin;
  const status = (error as { status?: number } | undefined)?.status;
  const tv = canManage && searchParams.get('tv') === '1';
  const isPlayer = game?.yourPlayerId != null;
  const tvLayout = tv || (canManage && !isPlayer);
  // A player whose seat was taken over from another device is an observer from now on (their cookie no longer counts)
  if (isPlayer && !wasPlayer) setWasPlayer(true);
  const lostSeat = wasPlayer && game !== undefined && !isPlayer && !canManage;

  // The server pushes every change of the lobby (ResistanceChanged), so other devices stay current without polling.
  // The TV view also follows who is connected; any other view is a player's (or observer's) own page and counts as
  // that player being online. The TV tab shares the player's cookie, so it must not, not even before the viewer's
  // role is known (`?tv=1` is the TV tab's own flag).
  // Whoever has the music queue (a player, the host and admins) also hears the music pushes.
  const hasQueue = isPlayer || canManage;
  const hubTopics = useMemo(() => {
    const base = tvLayout ? RESISTANCE_TV_HUB_TOPICS : RESISTANCE_HUB_TOPICS;
    return hasQueue ? withMusicTopic(base) : base;
  }, [tvLayout, hasQueue]);
  useSessionMusicHub(sessionId, game !== undefined, undefined, hubTopics, {
    presence: !tvLayout && searchParams.get('tv') !== '1',
  });
  const presence = useResistancePresence(sessionId, tvLayout && game !== undefined);

  // No lobby yet (or not open to guests): the games list for the host and admins, the session page for the rest
  if (status === HTTP_STATUS_CODE.NOT_FOUND) {
    return <Navigate to={canManage ? sessionGamesPath(sessionId) : sessionPath(sessionId)} replace />;
  }

  const showSwitch = isPlayer && !tv;
  const showPlayerView = isPlayer && !tv && (embedded || wide || view === 'player');
  // From 1024 px wide the player view is always there, so the right column is the board unless the queue is chosen
  const showQueue = isPlayer && !tv && !embedded && view === 'queue';
  const showBoard = !embedded && !showQueue && (!showPlayerView || wide);
  // The TV tab shares the player's cookies, so it must not show the role or the "Ty" badge of whoever is logged in
  const shownGame = game && tv ? { ...game, yourPlayerId: null, yourRole: null, yourCharacter: null, fellowDefectorId: null, fellowSpies: null, yourVote: null, yourMissionCard: null, yourPlotCards: [], yourReveals: [], yourPeeks: [], yourInquisition: null } : game;

  const players = game?.players ?? [];
  const started = game?.startedAt != null;
  const board = missionBoardFor(players.length, game?.rules);

  const back = canManage
    ? { to: sessionGamesPath(sessionId), label: t('resistance.page.backToGames') }
    : { to: sessionPath(sessionId), label: t('common.backToSession') };

  const openGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.openResistance(sessionId), { revalidate: false });
      void mutateGlobal(resistanceLobbyKey(sessionId));
    } catch {
      setMessage(t('resistance.errors.open'));
    }
  };

  const refreshRules = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.refreshResistanceRules(sessionId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.refreshRules'));
    }
  };

  const startGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.startResistance(sessionId), { revalidate: false });
      void mutateGlobal(resistanceLobbyKey(sessionId));
    } catch {
      setMessage(t('resistance.errors.start'));
    }
  };

  const submitTeam = async (playerIds: string[]) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.selectResistanceTeam(sessionId, playerIds), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.submitTeam'));
    }
  };

  const dealPlotCards = async (assignments: { cardId: number; playerId: string }[]) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.dealResistancePlotCards(sessionId, assignments), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.deal'));
    }
  };

  const playPlotCard = async (cardId: number, targetPlayerId: string) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.playResistancePlotCard(sessionId, cardId, targetPlayerId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.play'));
    }
  };

  const takeResponsibility = async (cardId: number, targetPlayerId: string, takeOpinionMaker: boolean | null) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.takeResistanceResponsibility(sessionId, cardId, targetPlayerId, takeOpinionMaker), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.take'));
    }
  };

  const takeLead = async (cardId: number) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.takeResistanceLead(sessionId, cardId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.takeLead'));
    }
  };

  const peekMissionCard = async (cardId: number, targetPlayerId: string) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.peekResistanceMissionCard(sessionId, cardId, targetPlayerId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.peek'));
    }
  };

  const playNoConfidence = async (cardId: number) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.playResistanceNoConfidence(sessionId, cardId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.play'));
    }
  };

  const passLeaderWindow = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.passResistanceLeaderWindow(sessionId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.confirmWindow'));
    }
  };

  const confirmPlotCard = async (cardId: number) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.confirmResistancePlotCard(sessionId, cardId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.confirmCard'));
    }
  };

  const examinePlayer = async (targetPlayerId: string) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.examineResistancePlayer(sessionId, targetPlayerId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.examine'));
    }
  };

  const confirmInquisition = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.confirmResistanceInquisition(sessionId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.confirmInquisition'));
    }
  };

  const assassinate = async (targetPlayerId: string) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.assassinateResistancePlayer(sessionId, targetPlayerId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.assassinate'));
    }
  };

  const castVote = async (approve: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.voteResistanceTeam(sessionId, approve), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.vote'));
    }
  };

  const playMissionCard = async (card: ResistanceMissionCardKind) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.playResistanceMissionCard(sessionId, card), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.missionCard'));
    }
  };

  const continueGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.continueResistance(sessionId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.next'));
    }
  };

  const restartGame = async () => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.restartResistance(sessionId), { revalidate: false });
    } catch {
      setMessage(t('resistance.errors.restart'));
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
      setMessage(t('resistance.errors.end'));
    }
  };

  const banner = shownGame && (
    <>
      <ResistancePhaseBanner
        text={resistancePhaseDescription(shownGame, { canManage, isAdmin }, board.teamSizes[0])}
        defectorFlips={shownGame.defectorFlips}
      />
      {!logHidden && <ResistancePlotLog cards={shownGame.playedPlotCards} players={shownGame.players} />}
    </>
  );

  const rulesButton = (
    <button
      type="button"
      onClick={() => setRulesOpen(true)}
      aria-label={t('resistance.page.rules')}
      title={t('resistance.page.rules')}
      className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
    >
      <InfoOutlinedIcon />
    </button>
  );

  const settingsButton = game && canManage && (
    <button
      type="button"
      onClick={() => setSettingsOpen(true)}
      disabled={started}
      aria-label={t('resistance.settings.title')}
      title={started ? t('resistance.settings.locked') : t('resistance.settings.title')}
      className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer disabled:opacity-50 disabled:cursor-default disabled:hover:text-slate-400"
    >
      <SettingsIcon />
    </button>
  );

  const devPanelsLink = import.meta.env.DEV && canManage && game && (
    <a
      href={devSessionResistancePanelsPath(sessionId)}
      target="_blank"
      rel="noopener noreferrer"
      className="px-2 py-1 text-xs text-slate-400 border border-dashed border-slate-600 rounded-md hover:text-slate-200"
    >
      {t('resistance.dev.open')}
    </a>
  );

  const trailingButtons = (
    <div className="ml-auto flex items-center gap-1">
      {devPanelsLink}
      {rulesButton}
      {settingsButton}
    </div>
  );

  const managerControls = game && canManage && (
    <ResistanceManagerControls
      game={game}
      isAdmin={isAdmin}
      onManagePlayers={() => setManagingPlayers(true)}
      onManageSeats={() => setSeatsOpen(true)}
      onEnd={() => setConfirmingEnd(true)}
      onStart={() => void startGame()}
      onOpen={() => void openGame()}
      onRefreshRules={() => void refreshRules()}
      onContinue={() => void continueGame()}
      onRestart={() => void restartGame()}
      logHidden={logHidden}
      onToggleLog={() => setLogHidden(!logHidden)}
    />
  );

  const playerCount = game && (
    <span className="text-slate-400">
      {t('resistance.page.playerCount', { current: players.length, max: game.maxPlayers })}
    </span>
  );

  return (
    <div
      className={
        tvLayout
          ? 'relative w-full h-screen bg-slate-800 text-slate-400'
          : `flex flex-col gap-3 w-full min-h-screen p-3 bg-slate-800 text-slate-400 ${embedded ? '' : 'pt-10'}`
      }
    >
      {tvLayout ? (
        <nav className="absolute inset-x-0 top-0 h-[10%] pl-[max(10%,5rem)] pr-[10%] flex items-center">
          <div className="flex items-center gap-2 w-full">
            <PageNavigation
              links={[
                back,
                ...(game && canManage && game.isOpen && !started && game.yourPlayerId == null
                  ? [{ to: sessionResistanceJoinPath(sessionId), label: t('resistance.page.joinAsPlayer') }]
                  : []),
              ]}
            />
            {managerControls}
            {game && (
              <button
                type="button"
                onClick={() => setQueueOpen(true)}
                className="px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700 cursor-pointer"
              >
                {t('resistance.controls.queue')}
              </button>
            )}
            {playerCount}
            {message && <span className="text-amber-500">{message}</span>}
            {trailingButtons}
          </div>
        </nav>
      ) : embedded ? (
        message && <span className="text-amber-500">{message}</span>
      ) : (
        <nav className="flex flex-wrap items-center gap-2">
          <PageNavigation links={[back]} />
          {showSwitch &&
            (wide ? (
              <ResistanceViewSwitch
                value={view === 'queue' ? 'queue' : 'board'}
                onChange={setView}
                views={WIDE_VIEWS}
              />
            ) : (
              <ResistanceViewSwitch value={view} onChange={setView} />
            ))}
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
              {t('resistance.controls.show')}
            </button>
          )}
          {trailingButtons}
        </nav>
      )}

      {!tvLayout && canManage && controlsOpen && (
        <div id="resistance-manager-controls" role="group" aria-label={t('resistance.controls.label')} className="flex flex-wrap gap-2">
          <a
            href={withDevPersona(`${sessionResistancePath(sessionId)}?tv=1`)}
            target="_blank"
            rel="noopener noreferrer"
            className="px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700"
          >
            {t('resistance.controls.openTv')}
          </a>
          {managerControls}
        </div>
      )}

      {lostSeat && (
        <p role="status" className="text-amber-500">
          {t('resistance.page.seatLost')}{' '}
          <Link to={sessionResistanceJoinPath(sessionId)} className="underline">
            {t('resistance.page.seatLostLink')}
          </Link>
        </p>
      )}

      {tvLayout ? (
        <ResistanceTvBoard
          game={game}
          board={board}
          banner={banner}
          yourPlayerId={shownGame?.yourPlayerId ?? null}
          onlinePlayerIds={presence?.onlinePlayerIds}
        />
      ) : (
        <>
          {!embedded && banner}
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
                  onExaminePlayer={examinePlayer}
                  onConfirmInquisition={confirmInquisition}
                  onAssassinate={assassinate}
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
            {showQueue && (
              <div className={wide ? 'flex-1 min-w-0' : undefined}>
                <ResistanceQueuePanel sessionId={sessionId} />
              </div>
            )}
          </div>
        </>
      )}

      {canManage && !started && (
        <ResistanceSettings sessionId={sessionId} open={managingPlayers} onClose={() => setManagingPlayers(false)} />
      )}

      {canManage && game && started && (
        <ResistanceSeatsDialog
          sessionId={sessionId}
          open={seatsOpen}
          onClose={() => setSeatsOpen(false)}
          players={game.players}
          onSetReleased={(player, released) =>
            mutate(gamesApi.setResistanceSeatReleased(sessionId, player.id, released), { revalidate: false })
          }
        />
      )}

      {canManage && <ResistanceGameSettings sessionId={sessionId} open={settingsOpen} onClose={() => setSettingsOpen(false)} />}

      {tvLayout && game && (
        <Dialog
          open={queueOpen}
          onClose={() => setQueueOpen(false)}
          fullWidth
          maxWidth="sm"
          aria-labelledby="resistance-queue-title"
          slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
        >
          <DialogTitle id="resistance-queue-title">{t('resistance.queue.title')}</DialogTitle>
          {/* Room for the search results, which drop below the form */}
          <DialogContent className="min-h-[60vh]">
            <ResistanceQueuePanel sessionId={sessionId} />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setQueueOpen(false)}>{t('common.close')}</Button>
          </DialogActions>
        </Dialog>
      )}

      <Dialog
        open={confirmingEnd}
        onClose={() => setConfirmingEnd(false)}
        aria-labelledby="end-resistance-title"
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        <DialogTitle id="end-resistance-title">{t('resistance.page.endTitle')}</DialogTitle>
        <DialogContent>{t('resistance.page.endBody')}</DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmingEnd(false)}>{t('common.cancel')}</Button>
          <Button color="error" onClick={() => void endGame()}>
            {t('resistance.controls.end')}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={rulesOpen}
        onClose={() => setRulesOpen(false)}
        aria-labelledby="resistance-rules-title"
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        <DialogTitle id="resistance-rules-title">{t('resistance.page.rulesTitle')}</DialogTitle>
        <DialogContent className="space-y-3">
          {resistanceRules().map((rule) => (
            <section key={rule.title}>
              <h2 className="text-slate-200">{rule.title}</h2>
              <p className="text-sm text-slate-400">{rule.text}</p>
              {rule.items && (
                <ul className="mt-1 list-disc space-y-1 pl-5 text-sm text-slate-400">
                  {rule.items.map((item) => (
                    <li key={item.name}>
                      <strong className="font-medium text-slate-200">{item.name}</strong> – {item.text}
                    </li>
                  ))}
                </ul>
              )}
              {rule.footer && <p className="mt-1 text-sm text-slate-400">{rule.footer}</p>}
            </section>
          ))}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => navigate(sessionResistanceIconsPath(sessionId))}>{t('resistance.page.iconGuide')}</Button>
          <Button onClick={() => setRulesOpen(false)}>{t('common.close')}</Button>
        </DialogActions>
      </Dialog>
    </div>
  );
};
