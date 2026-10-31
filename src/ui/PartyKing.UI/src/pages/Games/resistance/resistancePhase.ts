import { i18n } from '@/i18n';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { IMMEDIATE_PLOT_CARDS } from './resistancePlotCards';
import { MIN_PLAYERS } from './resistanceTeams';

const MAX_REJECTED_TEAMS = 5;

type Viewer = {
  /** Host or admin. */
  canManage: boolean;
  isAdmin: boolean;
};

const t = i18n.t.bind(i18n);

/** The confirmation line of a window in which everybody decides whether to play a plot card. */
const confirmation = (game: ResistanceGameDto): string => {
  if (game.yourPlayerId == null) return '';

  return ` ${t(game.leaderWindowPassedPlayerIds.includes(game.yourPlayerId) ? 'resistance.phase.confirmedMine' : 'resistance.phase.confirmPrompt')}`;
};

/**
 * What is going on in the game and what has to happen next, for the line above the mission board. It never mentions
 * the viewer's role, because it stays on the screen while the role card is face up. The text follows the UI language
 * at the time of the call.
 */
export const resistancePhaseDescription = (
  game: ResistanceGameDto,
  viewer: Viewer,
  firstTeamSize: number,
): string => {
  const count = game.players.length;
  const missing = MIN_PLAYERS - count;

  if (game.startedAt != null) {
    const leader = game.players.find((player) => player.id === game.leaderPlayerId);
    const youLead = leader != null && leader.id === game.yourPlayerId;
    const who = youLead
      ? t('resistance.phase.youLead')
      : t('resistance.phase.leaderIs', { name: leader?.name ?? t('resistance.phase.aPlayer') });
    const size = game.requiredTeamSize ?? firstTeamSize;
    const mission =
      game.missionNumber === 1
        ? t('resistance.phase.firstMission')
        : t('resistance.phase.missionN', { n: game.missionNumber });
    const card = game.yourPlayerId != null ? ` ${t('resistance.phase.tapCard')}` : '';

    const names = game.players.filter((player) => game.teamPlayerIds.includes(player.id)).map((player) => player.name);
    const rejected =
      game.rejectedTeams > 0
        ? t('resistance.phase.rejected', { count: game.rejectedTeams, max: MAX_REJECTED_TEAMS })
        : '';
    const votes = game.votes ?? [];
    const approvals = votes.filter((vote) => vote.approve).length;
    const tally = t('resistance.phase.tally', { approve: approvals, reject: votes.length - approvals });

    const results = game.missionResults ?? [];
    const succeeded = results.filter((result) => result.succeeded).length;
    const score = t('resistance.phase.score', { resistance: succeeded, spies: results.length - succeeded });
    const passed = game.leaderWindowPassedPlayerIds.length;

    if (game.phase === 'GameOver') {
      const why =
        game.winner === 'Spy'
          ? game.rejectedTeams >= MAX_REJECTED_TEAMS
            ? t('resistance.phase.whyRejected', { max: MAX_REJECTED_TEAMS })
            : t('resistance.phase.whyFailed')
          : t('resistance.phase.whySucceeded');
      const winner = t(game.winner === 'Spy' ? 'resistance.phase.spiesWon' : 'resistance.phase.resistanceWon');

      return t('resistance.phase.gameOver', { winner, why, score });
    }

    if (game.phase === 'DealingCards') {
      const task = t(youLead ? 'resistance.phase.dealYou' : 'resistance.phase.dealLeader');

      return t('resistance.phase.dealing', { who, task, mission });
    }

    if (game.phase === 'LeaderWindow') {
      return t('resistance.phase.leaderWindow', { who, passed, count, mine: confirmation(game) });
    }

    if (game.phase === 'ConfidenceWindow') {
      const openName = game.players.find((player) => player.id === game.openMissionPlayerId)?.name;
      const open = openName == null ? '' : ` ${t('resistance.phase.mustPlayOpen', { name: openName })}`;

      return t('resistance.phase.confidenceWindow', {
        names: names.join(', '),
        tally,
        passed,
        count,
        open,
        mine: confirmation(game),
      });
    }

    if (game.phase === 'ResolvingCards') {
      const waiting = game.playedPlotCards
        .filter((card) => IMMEDIATE_PLOT_CARDS.includes(card.type) && !card.isConfirmed)
        .map((card) => game.players.find((player) => player.id === (card.type === 'OverheardConversation' ? card.holderPlayerId : card.targetPlayerId))?.name)
        .filter((name) => name != null);
      const confirm = waiting.length > 0 ? ` ${t('resistance.phase.waitingConfirm', { names: waiting.join(', ') })}` : '';

      return t('resistance.phase.resolving', { who, confirm });
    }

    if (game.phase === 'PlayingMission') {
      const progress = t('resistance.phase.cardsPlayed', {
        played: game.playedMissionPlayerIds.length,
        total: game.teamPlayerIds.length,
      });
      const fails = game.requiredFails ?? 1;
      const rule = fails > 1 ? ` ${t('resistance.phase.failRule', { count: fails })}` : '';
      const onTeam = game.yourPlayerId != null && game.teamPlayerIds.includes(game.yourPlayerId);
      const mine = !onTeam
        ? ''
        : ` ${t(game.yourMissionCard == null ? 'resistance.phase.playYourCard' : 'resistance.phase.yourCardPlayed')}`;

      const openName = game.players.find((player) => player.id === game.openMissionPlayerId)?.name;
      const open = openName == null
        ? ''
        : game.openMissionCard == null
          ? ` ${t('resistance.phase.mustPlayOpen', { name: openName })}`
          : ` ${t('resistance.phase.playedOpen', {
              name: openName,
              card: t(game.openMissionCard ? 'resistance.missionCard.success' : 'resistance.missionCard.fail'),
            })}`;

      return t('resistance.phase.playingMission', { names: names.join(', '), tally, mission, progress, rule, mine, open });
    }

    const last = results.find((result) => result.missionNumber === game.missionNumber);
    const outcome = last?.succeeded
      ? t('resistance.phase.missionSucceeded')
      : t('resistance.phase.missionFailed', { count: last?.failCount ?? 0 });

    if (game.phase === 'PeekWindow') {
      return t('resistance.phase.peekWindow', { outcome, score, passed, count, mine: confirmation(game) });
    }

    if (game.phase === 'MissionResult') {
      const next = t(youLead || viewer.canManage ? 'resistance.phase.nextClick' : 'resistance.phase.nextWait');
      const fails =
        last?.succeeded && last.failCount > 0 ? ` ${t('resistance.phase.failCardsNote', { count: last.failCount })}` : '';

      return t('resistance.phase.missionResult', { outcome, fails, score, next });
    }

    if (game.phase === 'Voting') {
      const progress = t('resistance.phase.voted', { voted: game.votedPlayerIds.length, count });
      const mine =
        game.yourPlayerId == null
          ? ''
          : ` ${t(game.yourVote == null ? 'resistance.phase.voteNow' : 'resistance.phase.voteChange')}`;

      const makers = game.players.filter((player) => game.opinionMakerPlayerIds.includes(player.id));
      const pendingMakers = makers.filter((player) => !game.votedPlayerIds.includes(player.id));
      const order =
        pendingMakers.length > 0
          ? ` ${t('resistance.phase.makerFirst', { names: pendingMakers.map((player) => player.name).join(', ') })}`
          : '';

      return t('resistance.phase.voting', { who, mission, names: names.join(', '), progress, order, mine, card });
    }

    const task = youLead
      ? t('resistance.phase.pickYou', { count: size, mission })
      : t('resistance.phase.pickLeader', { count: size, mission });

    if (votes.length > 0) {
      return t('resistance.phase.rejectedPrev', {
        tally,
        rejected: rejected ? `${rejected} ` : '',
        who,
        task,
        card,
      });
    }

    return t('resistance.phase.started', { who, task, rejected: rejected ? ` ${rejected}` : '', card });
  }

  if (!game.isOpen) {
    const need = missing > 0 ? ` ${t('resistance.phase.needPlayers', { min: MIN_PLAYERS })}` : '';

    return t(viewer.isAdmin ? 'resistance.phase.closedAdmin' : 'resistance.phase.closedGuest', { need });
  }

  if (missing > 0) {
    const waiting = t(viewer.canManage ? 'resistance.phase.waitingHost' : 'resistance.phase.waitingGuest');

    return t('resistance.phase.notEnough', { waiting, min: MIN_PLAYERS, missing });
  }

  return t(viewer.canManage ? 'resistance.phase.readyManager' : 'resistance.phase.readyGuest');
};
