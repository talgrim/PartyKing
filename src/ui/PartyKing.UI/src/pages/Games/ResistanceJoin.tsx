import { ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { useSWRConfig } from 'swr';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceLobby } from '@/service/endpoints/games/useResistanceLobby';
import { resistanceKey, resistanceLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { GameJoin } from './lobby/GameJoin';
import { Resistance } from './Resistance';

/** Where a guest (or the host or an admin) joins the open Resistance game by name; once joined it shows the board. */
export const ResistanceJoin = (): ReactNode => {
  const { sessionId = '' } = useParams();
  const { mutate } = useSWRConfig();
  const { data: lobby, error, isLoading } = useResistanceLobby(sessionId);

  return (
    <GameJoin
      sessionId={sessionId}
      title="Dołącz do gry w Resistance"
      lobby={lobby}
      error={error}
      isLoading={isLoading}
      join={(name) => gamesApi.joinResistance(sessionId, name)}
      onJoined={() => {
        void mutate(resistanceLobbyKey(sessionId));
        void mutate(resistanceKey(sessionId));
      }}
      board={<Resistance />}
    />
  );
};
