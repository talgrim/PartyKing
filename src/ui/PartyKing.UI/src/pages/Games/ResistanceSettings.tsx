import { ReactNode } from 'react';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceGame } from '@/service/endpoints/games/useResistanceGame';
import { GamePlayersDialog } from './lobby/GamePlayersDialog';

type Props = {
  sessionId: string;
  open: boolean;
  onClose: () => void;
};

/** Host and admins: the seat order of the players (drag to change it) and a way to kick a player. */
export const ResistanceSettings = ({ sessionId, open, onClose }: Props): ReactNode => {
  const { data: game, mutate } = useResistanceGame(sessionId);

  return (
    <GamePlayersDialog
      open={open}
      onClose={onClose}
      players={game?.players ?? []}
      orderLabel="the seat order"
      onReorder={(reordered) =>
        mutate(
          gamesApi.reorderResistancePlayers(
            sessionId,
            reordered.map((player) => player.id),
          ),
          game && { optimisticData: { ...game, players: reordered }, rollbackOnError: true, revalidate: false },
        )
      }
      onKick={(player) => mutate(gamesApi.kickResistancePlayer(sessionId, player.id), { revalidate: false })}
      onReload={() => void mutate()}
    />
  );
};
