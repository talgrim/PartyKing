import { ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { useSWRConfig } from 'swr';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceClaimableSeats } from '@/service/endpoints/games/useResistanceClaimableSeats';
import { useResistanceLobby } from '@/service/endpoints/games/useResistanceLobby';
import { resistanceKey, resistanceLobbyKey, RESISTANCE_JOIN_HUB_TOPICS } from '@/service/realtime/sessionMusicEvents';
import { GameJoin } from './lobby/GameJoin';
import { Resistance } from './Resistance';
import { useTranslation } from 'react-i18next';

/**
 * Where a guest (or the host or an admin) joins the open Resistance game by name; once joined it shows the board. Once the
 * game started it offers the seats the host released, for a player who lost their device to take over.
 */
export const ResistanceJoin = (): ReactNode => {
  const { t } = useTranslation();
  const { sessionId = '' } = useParams();
  const { mutate } = useSWRConfig();
  const { data: lobby, error, isLoading } = useResistanceLobby(sessionId);
  const started = lobby?.isStarted === true && !lobby.joined;
  const { seats, mutate: reloadSeats } = useResistanceClaimableSeats(sessionId, started);

  const revalidate = () => {
    void mutate(resistanceLobbyKey(sessionId));
    void mutate(resistanceKey(sessionId));
  };

  return (
    <GameJoin
      topics={RESISTANCE_JOIN_HUB_TOPICS}
      sessionId={sessionId}
      title={t('resistance.joinTitle')}
      lobby={lobby}
      error={error}
      isLoading={isLoading}
      join={(name) => gamesApi.joinResistance(sessionId, name)}
      onJoined={revalidate}
      returnToGame={{
        seats,
        claim: (playerId) => gamesApi.claimResistanceSeat(sessionId, playerId),
        onReload: () => void reloadSeats(),
      }}
      board={<Resistance />}
    />
  );
};
