import { FormEvent, ReactNode, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { useSWRConfig } from 'swr';
import { Button, TextField } from '@mui/material';
import { sessionPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useRingoLobby } from '@/service/endpoints/games/useRingoLobby';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import { ringoKey, ringoLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import { Ringo } from './Ringo';

const MAX_NAME_LENGTH = 30;

const joinErrorMessage = (error: unknown): string => {
  switch ((error as { status?: number } | undefined)?.status) {
    case HTTP_STATUS_CODE.CONFLICT:
      return 'Ktoś już używa tego imienia (albo gra jest zamknięta).';
    case HTTP_STATUS_CODE.BAD_REQUEST:
      return 'To imię jest niepoprawne.';
    default:
      return 'Nie udało się dołączyć do gry.';
  }
};

type FormProps = {
  sessionId: string;
  isManager: boolean;
  kicked: boolean;
  onJoined: () => void;
};

const JoinForm = ({ sessionId, isManager, kicked, onJoined }: FormProps): ReactNode => {
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await gamesApi.joinRingo(sessionId, name.trim());
      onJoined();
    } catch (e) {
      setError(joinErrorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={(event) => void submit(event)} className="flex flex-col gap-3 p-4 max-w-sm mx-auto">
      <h1 className="text-xl text-slate-200">Dołącz do gry w ringo</h1>
      {kicked && <p className="text-amber-500">Usunięto cię z gry.</p>}
      {!isManager && (
        <TextField
          label="Twoje imię"
          value={name}
          onChange={(event) => setName(event.target.value)}
          slotProps={{ htmlInput: { maxLength: MAX_NAME_LENGTH } }}
          autoFocus
          required
        />
      )}
      {error && <p className="text-amber-500">{error}</p>}
      <Button type="submit" variant="contained" disabled={busy || (!isManager && name.trim() === '')}>
        Dołącz
      </Button>
    </form>
  );
};

/** Where a guest (or the host or an admin) joins the open Ringo game by name; once joined it shows the board. */
export const RingoJoin = (): ReactNode => {
  const { sessionId = '' } = useParams();
  const { mutate } = useSWRConfig();
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const { data: lobby, error, isLoading } = useRingoLobby(sessionId);
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
    return <Ringo />;
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
    <JoinForm
      sessionId={sessionId}
      isManager={isHost || isAdmin}
      kicked={wasJoined}
      onJoined={() => {
        void mutate(ringoLobbyKey(sessionId));
        void mutate(ringoKey(sessionId));
      }}
    />
  );
};
