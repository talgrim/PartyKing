import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistanceRoleCard } from '../ResistanceRoleCard';
import { ResistanceTeamPicker } from './ResistanceTeamPicker';
import { ResistanceMissionPanel } from './ResistanceMissionPanel';
import { ResistanceVotePanel } from './ResistanceVotePanel';
import { ResistancePlotDealer } from './ResistancePlotDealer';
import { ResistancePlotHand } from './ResistancePlotHand';
import { ResistancePlotPlayer } from './ResistancePlotPlayer';

type Props = {
  game: ResistanceGameDto;
  /** Submits the leader's team; only the leader of a started game that is selecting a team sees the picker. */
  onSubmitTeam: (playerIds: string[]) => Promise<void>;
  /** Votes on the submitted team (true approves); only a player sees the vote buttons, and only while the vote is on. */
  onVote: (approve: boolean) => Promise<void>;
  /** Hands the drawn plot cards to the other players; only the leader sees the dealer, and only while dealing. */
  onDealPlotCards: (assignments: { cardId: number; playerId: string }[]) => Promise<void>;
  /** Plays an immediate plot card on a player; only its holder sees the picker, and only while the cards are played. */
  onPlayPlotCard: (cardId: number, targetPlayerId: string) => Promise<void>;
  /** Confirms having seen the identity card a played plot card showed. */
  onConfirmPlotCard: (cardId: number) => Promise<void>;
  /** Plays a mission card (true = Success); only a member of the approved team sees the cards. */
  onPlayMissionCard: (success: boolean) => Promise<void>;
  /** Moves on after a finished mission; the leader sees the button then. */
  onContinue: () => Promise<void>;
};

/** The view of a player: their secret role and, as the round goes, the team picker, the vote or the continue button. */
export const ResistancePlayerView = ({ game, onSubmitTeam, onDealPlotCards, onPlayPlotCard, onConfirmPlotCard, onVote, onPlayMissionCard, onContinue }: Props): ReactNode => {
  const isPlayer = game.yourPlayerId != null;
  const leads = isPlayer && game.yourPlayerId === game.leaderPlayerId;
  const deals = leads && game.phase === 'DealingCards';
  const resolves = isPlayer && game.phase === 'ResolvingCards';
  const picksTeam = leads && game.phase === 'SelectingTeam' && game.requiredTeamSize != null;
  const votes = isPlayer && game.phase === 'Voting';
  const plays = isPlayer && game.phase === 'PlayingMission' && game.teamPlayerIds.includes(game.yourPlayerId!);
  const continues = leads && game.phase === 'MissionResult';

  return (
    <div className="flex flex-col items-center gap-4 py-2">
      {game.yourRole != null && (
        <ResistanceRoleCard role={game.yourRole} players={game.players} fellowSpyIds={game.fellowSpies ?? []} />
      )}
      {deals && (
        <ResistancePlotDealer
          key={game.missionNumber}
          leaderId={game.yourPlayerId!}
          players={game.players}
          cards={game.yourPlotCards.filter((card) => card.toDeal)}
          onSubmit={onDealPlotCards}
        />
      )}
      {resolves && <ResistancePlotPlayer game={game} onPlay={onPlayPlotCard} onConfirm={onConfirmPlotCard} />}
      <ResistancePlotHand cards={game.yourPlotCards} />
      {picksTeam && (
        <ResistanceTeamPicker
          key={`${game.missionNumber}-${game.rejectedTeams}`}
          players={game.players}
          size={game.requiredTeamSize!}
          onSubmit={onSubmitTeam}
        />
      )}
      {votes && <ResistanceVotePanel game={game} onVote={onVote} />}
      {plays && <ResistanceMissionPanel game={game} onPlay={onPlayMissionCard} />}
      {continues && (
        <button
          type="button"
          onClick={() => void onContinue()}
          className="w-full rounded-lg bg-emerald-500 px-4 py-3 font-semibold text-slate-900 shadow transition-colors hover:bg-emerald-400"
        >
          Dalej
        </button>
      )}
    </div>
  );
};
