import { ReactNode } from 'react';
import { sessionResistanceJoinPath } from '@/router/constants';
import { useResistanceLobby } from '@/service/endpoints/games/useResistanceLobby';
import { GameInvite } from './lobby/GameInvite';
import { useTranslation } from 'react-i18next';

const dismissedKey = (sessionId: string, createdAt: string | number) =>
  `partyking.resistanceInviteDismissed.${sessionId}.${createdAt}`;

type Props = {
  sessionId: string;
  enabled: boolean;
};

/** An invitation for a guest while the session's Resistance game is open and they have not joined it. */
export const ResistanceInvite = ({ sessionId, enabled }: Props): ReactNode => {
  const { t } = useTranslation();
  const { data: lobby } = useResistanceLobby(sessionId, enabled);

  return (
    <GameInvite
      sessionId={sessionId}
      lobby={lobby}
      gameId={lobby?.createdAt ?? ''}
      dismissedKey={dismissedKey}
      joinPath={sessionResistanceJoinPath(sessionId)}
      message={t('resistance.inviteMessage')}
    />
  );
};
