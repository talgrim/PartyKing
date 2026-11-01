import { FormEvent, ReactNode, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { Button, TextField } from '@mui/material';
import { i18n } from '@/i18n';
import { sessionPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import { SessionHubTopic } from '@/service/realtime/sessionMusicEvents';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import { useTranslation } from 'react-i18next';

const MAX_NAME_LENGTH = 30;

const joinErrorMessage = (error: unknown): string => {
  switch ((error as { status?: number } | undefined)?.status) {
    case HTTP_STATUS_CODE.CONFLICT:
      return i18n.t('lobby.joinConflict');
    case HTTP_STATUS_CODE.BAD_REQUEST:
      return i18n.t('lobby.joinBadName');
    default:
      return i18n.t('lobby.joinFailed');
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
  const { t } = useTranslation();
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
    <form onSubmit={(event) => void submit(event)} className="flex flex-col gap-3 p-4 pt-10 max-w-sm mx-auto">
      <h1 className="text-xl text-slate-200">{title}</h1>
      {kicked && <p className="text-amber-500">{t('lobby.kicked')}</p>}
      {!isHost && (
        <TextField
          label={nameRequired ? t('lobby.nameLabel') : t('lobby.nameOptional')}
          helperText={nameRequired ? undefined : t('lobby.adminHint')}
          value={name}
          onChange={(event) => setName(event.target.value)}
          slotProps={{ htmlInput: { maxLength: MAX_NAME_LENGTH } }}
          autoFocus
          required={nameRequired}
        />
      )}
      {error && <p className="text-amber-500">{error}</p>}
      <Button type="submit" variant="contained" disabled={busy || (nameRequired && name.trim() === '')}>
        {t('lobby.join')}
      </Button>
    </form>
  );
};

type Props = {
  sessionId: string;
  /** The hub topic of the game, so only its pushes reach this page. */
  topic: SessionHubTopic;
  title: string;
  lobby: { isOpen: boolean; isStarted?: boolean; joined: boolean } | undefined;
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
export const GameJoin = ({ sessionId, topic, title, lobby, error, isLoading, join, onJoined, board }: Props): ReactNode => {
  const { t } = useTranslation();
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const [wasJoined, setWasJoined] = useState(false);
  const joined = lobby?.joined === true;

  // The board opens its own hub connection; before that this page needs one to see the game open or a kick
  useSessionMusicHub(sessionId, !joined, undefined, [topic]);

  const status = (error as { status?: number } | undefined)?.status;
  if (status === HTTP_STATUS_CODE.NOT_FOUND || status === HTTP_STATUS_CODE.UNAUTHORIZED) {
    return <Navigate to={sessionPath(sessionId)} replace />;
  }

  if (joined) {
    if (!wasJoined) setWasJoined(true);
    return board;
  }
  if (isLoading || !lobby) return null;

  if (lobby.isStarted && !wasJoined) {
    return (
      <p className="p-4 pt-10 text-slate-200">
        {t('lobby.started')}{' '}
        <Link to={sessionPath(sessionId)} className="underline">
          {t('common.backToSession')}
        </Link>
      </p>
    );
  }

  if (!lobby.isOpen && !wasJoined) {
    return (
      <p className="p-4 pt-10 text-slate-200">
        {t('lobby.notOpen')}{' '}
        <Link to={sessionPath(sessionId)} className="underline">
          {t('common.backToSession')}
        </Link>
      </p>
    );
  }

  return (
    <JoinForm title={title} isHost={isHost} isAdmin={isAdmin} kicked={wasJoined} join={join} onJoined={onJoined} />
  );
};
