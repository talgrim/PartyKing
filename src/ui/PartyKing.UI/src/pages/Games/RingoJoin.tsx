import { ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { useSWRConfig } from 'swr';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useRingoLobby } from '@/service/endpoints/games/useRingoLobby';
import { ringoKey, ringoLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { GameJoin } from './lobby/GameJoin';
import { Ringo } from './Ringo';
import { useTranslation } from 'react-i18next';

/** Where a guest (or the host or an admin) joins the open Ringo game by name; once joined it shows the board. */
export const RingoJoin = (): ReactNode => {
  const { t } = useTranslation();
  const { sessionId = '' } = useParams();
  const { mutate } = useSWRConfig();
  const { data: lobby, error, isLoading } = useRingoLobby(sessionId);

  return (
    <GameJoin
      sessionId={sessionId}
      title={t('ringo.joinTitle')}
      lobby={lobby}
      error={error}
      isLoading={isLoading}
      join={(name) => gamesApi.joinRingo(sessionId, name)}
      onJoined={() => {
        void mutate(ringoLobbyKey(sessionId));
        void mutate(ringoKey(sessionId));
      }}
      board={<Ringo />}
    />
  );
};
