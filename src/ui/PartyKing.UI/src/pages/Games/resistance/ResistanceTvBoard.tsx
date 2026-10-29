import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistancePlayers } from '../ResistancePlayers';
import { ResistanceMissionBoard } from './ResistanceMissionBoard';
import { MissionBoardLayout } from './resistanceTeams';

type Props = {
  game: ResistanceGameDto | undefined;
  board: MissionBoardLayout;
  banner: ReactNode;
  /** Marks the viewer's own seat; the TV tab shows no one as "you". */
  yourPlayerId: string | null;
};

/** The board for a big screen: the board and the phase line keep to the middle 80%, the players sit in the margins. */
export const ResistanceTvBoard = ({ game, board, banner, yourPlayerId }: Props): ReactNode => (
  <>
    <div className="absolute inset-[10%] flex flex-col items-center justify-center gap-4 overflow-hidden">
      {banner}
      <ResistanceMissionBoard board={board} className="w-full max-h-full" />
    </div>

    {game && game.players.length > 0 && (
      <ResistancePlayers players={game.players} yourPlayerId={yourPlayerId} leaderId={game.leaderPlayerId} />
    )}
  </>
);
