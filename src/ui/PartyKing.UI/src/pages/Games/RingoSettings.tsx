import { ReactNode } from 'react';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useRingoGame } from '@/service/endpoints/games/useRingoGame';
import { GamePlayersDialog } from './lobby/GamePlayersDialog';

type Props = {
  sessionId: string;
  open: boolean;
  onClose: () => void;
};

/** Host and admins: the turn order of the players (drag to change it) and a way to kick a player. */
export const RingoSettings = ({ sessionId, open, onClose }: Props): ReactNode => {
  const { data: game, mutate } = useRingoGame(sessionId);

  return (
    <GamePlayersDialog
      open={open}
      onClose={onClose}
      players={game?.players ?? []}
      currentPlayerId={game?.currentPlayerId}
      orderLabel="the turn order"
      onReorder={(reordered) =>
        mutate(
          gamesApi.reorderRingoPlayers(
            sessionId,
            reordered.map((player) => player.id),
          ),
          game && { optimisticData: { ...game, players: reordered }, rollbackOnError: true, revalidate: false },
        )
      }
      onKick={(player) => mutate(gamesApi.kickRingoPlayer(sessionId, player.id), { revalidate: false })}
      onReload={() => void mutate()}
    />
  );
};
