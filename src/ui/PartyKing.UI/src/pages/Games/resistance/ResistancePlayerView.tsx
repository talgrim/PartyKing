import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistanceRoleCard } from '../ResistanceRoleCard';
import { ResistanceTeamPicker } from './ResistanceTeamPicker';

type Props = {
  game: ResistanceGameDto;
  /** Submits the leader's team; only the leader of a started game without a team sees the picker. */
  onSubmitTeam: (playerIds: string[]) => Promise<void>;
};

/** The view of a player: their secret role and, while they lead the mission, the team picker. */
export const ResistancePlayerView = ({ game, onSubmitTeam }: Props): ReactNode => {
  const picksTeam =
    game.startedAt != null &&
    game.yourPlayerId != null &&
    game.yourPlayerId === game.leaderPlayerId &&
    game.requiredTeamSize != null &&
    game.teamPlayerIds.length === 0;

  return (
    <div className="flex flex-col items-center gap-4 py-2">
      {game.yourRole != null && (
        <ResistanceRoleCard role={game.yourRole} players={game.players} fellowSpyIds={game.fellowSpies ?? []} />
      )}
      {picksTeam && (
        <ResistanceTeamPicker players={game.players} size={game.requiredTeamSize!} onSubmit={onSubmitTeam} />
      )}
    </div>
  );
};
