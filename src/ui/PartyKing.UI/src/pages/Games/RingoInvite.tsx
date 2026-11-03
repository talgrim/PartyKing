import { ReactNode } from 'react';
import { sessionRingoJoinPath } from '@/router/constants';
import { useRingoLobby } from '@/service/endpoints/games/useRingoLobby';
import { GameInvite } from './lobby/GameInvite';
import { useTranslation } from 'react-i18next';

const dismissedKey = (sessionId: string, seed: string | number) => `partyking.ringoInviteDismissed.${sessionId}.${seed}`;

type Props = {
  sessionId: string;
  enabled: boolean;
};

/** An invitation for a guest while the session's Ringo game is open and they have not joined it. */
export const RingoInvite = ({ sessionId, enabled }: Props): ReactNode => {
  const { t } = useTranslation();
  const { data: lobby } = useRingoLobby(sessionId, enabled);

  return (
    <GameInvite
      sessionId={sessionId}
      lobby={lobby}
      gameId={lobby?.seed ?? 0}
      dismissedKey={dismissedKey}
      joinPath={sessionRingoJoinPath(sessionId)}
      message={t('ringo.inviteMessage')}
    />
  );
};
