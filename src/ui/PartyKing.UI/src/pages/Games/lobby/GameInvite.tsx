import { ReactNode, useState } from 'react';
import { Button, Snackbar } from '@mui/material';
import { withDevPersona } from '@/service/devPersona';

const wasDismissed = (key: string): boolean => {
  try {
    return sessionStorage.getItem(key) === '1';
  } catch {
    return false;
  }
};

type Props = {
  sessionId: string;
  /** What the guest is invited to, as the lobby reports it; undefined while loading or without a game. */
  lobby: { isOpen: boolean; isStarted?: boolean; joined: boolean } | undefined;
  /** Tells one game of the session from the next, so a dismissed invitation does not hide the next game's. */
  gameId: string | number;
  /** `sessionStorage` key of the dismissal for a session and game. */
  dismissedKey: (sessionId: string, gameId: string | number) => string;
  joinPath: string;
  message: string;
};

/** A non-modal invitation for a guest while a game of the session is open and they have not joined it. */
export const GameInvite = ({ sessionId, lobby, gameId, dismissedKey, joinPath, message }: Props): ReactNode => {
  const [dismissedNow, setDismissedNow] = useState<string>();

  if (!lobby || !lobby.isOpen || lobby.isStarted || lobby.joined) return null;

  const key = dismissedKey(sessionId, gameId);
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
    window.open(withDevPersona(joinPath), '_blank', 'noopener,noreferrer');
    dismiss();
  };

  return (
    <Snackbar
      open
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      message={message}
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
