import { ReactNode, useState } from 'react';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { adminApi } from '@/service/endpoints/admin/adminApi';
import { playerApi } from '@/service/endpoints/player/playerApi';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import { queueApi } from '@/service/endpoints/queue/queueApi';
import { PlayerControls } from './PlayerControls';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
  /** An admin skips through the admin endpoint, which also checks that a player is connected; a host through their own. */
  isAdmin: boolean;
  nowPlaying: NowPlayingDto | undefined;
  /** Called after a command went through, so the page can refresh what is playing. */
  onChanged: () => void;
  /** The volume lives in the host's browser, so only the host is given it. */
  volume?: number;
  onVolumeChange?: (volume: number) => void;
};

/**
 * Controls for an admin who is not the host, or for the host on a page that does not run the player. The music plays
 * in the host's browser tab, so the commands go through the server, and they do nothing unless that player is
 * connected.
 */
export const RemotePlayerControls = ({
  sessionId,
  isAdmin,
  nowPlaying,
  onChanged,
  volume,
  onVolumeChange,
}: Props): ReactNode => {
  const { t } = useTranslation();
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
        setError(t('nowPlaying.playerNotConnected'));
      } else if (status !== HTTP_STATUS_CODE.NOT_FOUND) {
        // 404 is an empty queue: the song ended, which is not a failure
        setError(t('adminPlayer.commandFailed'));
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
        onToggle={() => void run(() => playerApi.sendCommand(sessionId, paused ? 'Resume' : 'Pause'))}
        volume={volume}
        onVolumeChange={onVolumeChange}
        onNext={() => void run(() => (isAdmin ? adminApi.skipSong(sessionId, song?.id) : queueApi.next(sessionId, song?.id)))}
      />
      {error && <p className="px-2 pt-2 text-sm text-amber-500">{error}</p>}
    </>
  );
};
