import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { NowPlayingLine } from '@/components/Music/NowPlayingLine';
import { usePlayerVolume } from '@/components/Music/playerVolume';
import { Queue } from '@/components/Music/Queue';
import { RemotePlayerControls } from '@/components/Music/RemotePlayerControls';
import { useNowPlaying } from '@/service/endpoints/player/useNowPlaying';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';

type Props = {
  sessionId: string;
};

/**
 * The session's music inside the game: what is playing (as text) and the queue with the form to add a song, for
 * everyone who unlocked the session. The host and admins can also pause, resume and skip, and remove songs; the host
 * alone sets the volume, which lives in their browser. The music itself plays in the host's tab on the session page.
 */
export const ResistanceQueuePanel = ({ sessionId }: Props): ReactNode => {
  const { t } = useTranslation();
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const { data: nowPlaying, mutate: refreshNowPlaying } = useNowPlaying(sessionId);
  const [volume, setVolume] = usePlayerVolume();

  const canManage = isHost || isAdmin;

  return (
    <section aria-label={t('resistance.queue.title')} className="flex flex-col gap-3">
      <NowPlayingLine nowPlaying={nowPlaying} />
      {canManage && (
        <RemotePlayerControls
          sessionId={sessionId}
          isAdmin={isAdmin}
          nowPlaying={nowPlaying}
          onChanged={() => void refreshNowPlaying()}
          volume={isHost ? volume : undefined}
          onVolumeChange={isHost ? setVolume : undefined}
        />
      )}
      <Queue sessionId={sessionId} canRemove={canManage} />
    </section>
  );
};
