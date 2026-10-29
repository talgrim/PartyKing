import { ReactNode, useState } from 'react';
import { Button, Snackbar } from '@mui/material';
import { sessionRingoJoinPath } from '@/router/constants';
import { useRingoLobby } from '@/service/endpoints/games/useRingoLobby';

const dismissedKey = (sessionId: string, seed: number) => `partyking.ringoInviteDismissed.${sessionId}.${seed}`;

const wasDismissed = (key: string): boolean => {
  try {
    return sessionStorage.getItem(key) === '1';
  } catch {
    return false;
  }
};

type Props = {
  sessionId: string;
  enabled: boolean;
};

/** A non-modal invitation for a guest while the session's Ringo game is open and they have not joined it. */
export const RingoInvite = ({ sessionId, enabled }: Props): ReactNode => {
  const { data: lobby } = useRingoLobby(sessionId, enabled);
  const [dismissedNow, setDismissedNow] = useState<string>();

  if (!lobby || !lobby.isOpen || lobby.joined) return null;

  const key = dismissedKey(sessionId, lobby.seed);
  if (dismissedNow === key || wasDismissed(key)) return null;

  const dismiss = () => {
    try {
      sessionStorage.setItem(key, '1');
    } catch {
      // The invitation then comes back after a reload, which is fine
    }
    setDismissedNow(key);
  };

  const accept = () => {
    window.open(sessionRingoJoinPath(sessionId), '_blank', 'noopener,noreferrer');
    dismiss();
  };

  return (
    <Snackbar
      open
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      message="Nowa gra w ringo została uruchomiona. Czy chcesz dołączyć?"
      action={
        <>
          <Button color="inherit" size="small" onClick={accept}>
            Tak
          </Button>
          <Button color="inherit" size="small" onClick={dismiss}>
            Nie
          </Button>
        </>
      }
    />
  );
};
