import { ReactNode, useState } from 'react';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { adminApi } from '@/service/endpoints/admin/adminApi';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import { PlayerControls } from './PlayerControls';

type Props = {
  sessionId: string;
  nowPlaying: NowPlayingDto | undefined;
  /** Called after a command went through, so the page can refresh what is playing. */
  onChanged: () => void;
};

/**
 * Controls for an admin who is not the host. The music plays in the host's browser, so the commands go through the
 * server, and they do nothing unless that player is connected.
 */
export const AdminPlayerControls = ({ sessionId, nowPlaying, onChanged }: Props): ReactNode => {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = async (command: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await command();
    } catch (e) {
      const status = (e as { status?: number })?.status;
      if (status === HTTP_STATUS_CODE.CONFLICT) {
        setError("The host's player is not connected.");
      } else if (status !== HTTP_STATUS_CODE.NOT_FOUND) {
        // 404 is an empty queue: the song ended, which is not a failure
        setError('The command failed');
      }
    } finally {
      setBusy(false);
      onChanged();
    }
  };

  const paused = nowPlaying?.paused ?? false;
  const song = nowPlaying?.song ?? null;

  return (
    <>
      <PlayerControls
        paused={paused}
        disabled={busy || !nowPlaying?.playerConnected}
        hasSong={song !== null}
        onToggle={() => void run(() => adminApi.sendPlayerCommand(sessionId, paused ? 'Resume' : 'Pause'))}
        onNext={() => void run(() => adminApi.skipSong(sessionId, song?.id))}
      />
      {error && <p className="px-2 pt-2 text-sm text-amber-500">{error}</p>}
    </>
  );
};
