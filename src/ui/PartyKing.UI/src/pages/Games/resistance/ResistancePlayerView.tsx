import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistanceRoleCard } from '../ResistanceRoleCard';

type Props = {
  game: ResistanceGameDto;
};

/** The simple view of a player: their secret role (the voting and team buttons will join it with the missions). */
export const ResistancePlayerView = ({ game }: Props): ReactNode => (
  <div className="flex flex-col items-center gap-4 py-2">
    {game.yourRole != null && (
      <ResistanceRoleCard role={game.yourRole} players={game.players} fellowSpyIds={game.fellowSpies ?? []} />
    )}
  </div>
);
