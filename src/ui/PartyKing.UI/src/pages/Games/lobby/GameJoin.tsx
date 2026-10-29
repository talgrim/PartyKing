import { FormEvent, ReactNode, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { Button, TextField } from '@mui/material';
import { sessionPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';

const MAX_NAME_LENGTH = 30;

const joinErrorMessage = (error: unknown): string => {
  switch ((error as { status?: number } | undefined)?.status) {
    case HTTP_STATUS_CODE.CONFLICT:
      return 'Ktoś już używa tego imienia (albo gra jest zamknięta lub pełna).';
    case HTTP_STATUS_CODE.BAD_REQUEST:
      return 'To imię jest niepoprawne.';
    default:
      return 'Nie udało się dołączyć do gry.';
  }
};

type FormProps = {
  title: string;
  isHost: boolean;
  isAdmin: boolean;
  kicked: boolean;
  join: (name: string) => Promise<void>;
  onJoined: () => void;
};

const JoinForm = ({ title, isHost, isAdmin, kicked, join, onJoined }: FormProps): ReactNode => {
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  // The host joins under the name of their Spotify profile, an admin may skip the name ("admin"), a guest needs one
  const nameRequired = !isHost && !isAdmin;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await join(name.trim());
      onJoined();
    } catch (e) {
      setError(joinErrorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={(event) => void submit(event)} className="flex flex-col gap-3 p-4 max-w-sm mx-auto">
      <h1 className="text-xl text-slate-200">{title}</h1>
      {kicked && <p className="text-amber-500">Usunięto cię z gry.</p>}
      {!isHost && (
        <TextField
          label={nameRequired ? 'Twoje imię' : 'Twoje imię (opcjonalnie)'}
          helperText={nameRequired ? undefined : 'Bez imienia dołączysz jako „admin”.'}
          value={name}
          onChange={(event) => setName(event.target.value)}
          slotProps={{ htmlInput: { maxLength: MAX_NAME_LENGTH } }}
          autoFocus
          required={nameRequired}
        />
      )}
      {error && <p className="text-amber-500">{error}</p>}
      <Button type="submit" variant="contained" disabled={busy || (nameRequired && name.trim() === '')}>
        Dołącz
      </Button>
    </form>
  );
};

type Props = {
  sessionId: string;
  title: string;
  lobby: { isOpen: boolean; joined: boolean } | undefined;
  error: unknown;
  isLoading: boolean;
  join: (name: string) => Promise<void>;
  /** Revalidates what the game's pages show once the caller joined. */
  onJoined: () => void;
  /** The board, shown once the caller joined. */
  board: ReactNode;
};

/**
 * Where a guest (or the host or an admin) joins an open game by name; once joined it shows the board. A game that
 * does not exist or a locked session sends the caller back to the session page.
 */
export const GameJoin = ({ sessionId, title, lobby, error, isLoading, join, onJoined, board }: Props): ReactNode => {
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const [wasJoined, setWasJoined] = useState(false);
  const joined = lobby?.joined === true;

  // The board opens its own hub connection; before that this page needs one to see the game open or a kick
  useSessionMusicHub(sessionId, !joined);

  const status = (error as { status?: number } | undefined)?.status;
  if (status === HTTP_STATUS_CODE.NOT_FOUND || status === HTTP_STATUS_CODE.UNAUTHORIZED) {
    return <Navigate to={sessionPath(sessionId)} replace />;
  }

  if (joined) {
    if (!wasJoined) setWasJoined(true);
    return board;
  }
  if (isLoading || !lobby) return null;

  if (!lobby.isOpen && !wasJoined) {
    return (
      <p className="p-4 text-slate-200">
        Ta gra nie jest jeszcze otwarta.{' '}
        <Link to={sessionPath(sessionId)} className="underline">
          Wróć do sesji
        </Link>
      </p>
    );
  }

  return (
    <JoinForm title={title} isHost={isHost} isAdmin={isAdmin} kicked={wasJoined} join={join} onJoined={onJoined} />
  );
};
