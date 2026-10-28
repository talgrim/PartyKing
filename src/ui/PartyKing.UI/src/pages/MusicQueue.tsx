import { ReactNode, useEffect, useRef, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import useSWR, { useSWRConfig } from 'swr';
import { AdminPlayerControls } from '@/components/Music/AdminPlayerControls';
import { LyricsView } from '@/components/Music/LyricsView';
import { MaximizedView } from '@/components/Music/MaximizedView';
import { HostPlayer } from '@/components/Music/HostPlayer';
import { NowPlayingCard } from '@/components/Music/NowPlayingCard';
import { PhotoUploadDialog } from '@/components/Music/PhotoUploadDialog';
import { Queue } from '@/components/Music/Queue';
import { SessionHeader } from '@/components/Music/SessionHeader';
import { SlideshowControls } from '@/components/Music/SlideshowControls';
import { useUploadDialog } from '@/components/Music/useUploadDialog';
import { SessionPasswordForm } from '@/components/Music/SessionPasswordForm';
import { PageNavigation } from '@/components/common/PageNavigation';
import { SlideshowView } from '@/pages/Slideshow/SlideshowView';
import { ROUTE_PATHS } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { useLyrics } from '@/service/endpoints/lyrics/useLyrics';
import { useNowPlaying } from '@/service/endpoints/player/useNowPlaying';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import { HostPlayerPushHandlers } from '@/service/realtime/sessionMusicEvents';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import { SessionHubContext } from '@/service/realtime/SessionHubContext';
import { usePersistedFlag } from '@/utils/usePersistedFlag';

const JOIN_PARAM = 'join';

/**
 * Page of a session, behind the session password. Anyone who entered it (admins included) sees what is playing and
 * can search for or paste songs to add to the queue, no login needed. The host also gets the player, and an admin
 * the controls to pause and skip. The host and admins also get the slideshow in a drawer on the left, which can
 * be hidden or maximized.
 */
export const MusicQueue = (): ReactNode => {
  const { sessionId = '' } = useParams();

  const { mutate } = useSWRConfig();
  const [slideshowOpen, setSlideshowOpen] = usePersistedFlag('partyking.slideshowOpen', true);
  const [showLyrics, setShowLyrics] = usePersistedFlag('partyking.showLyrics', false);
  const [maximized, setMaximized] = useState(false);
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const uploadDialog = useUploadDialog();
  const { data: nowPlaying, mutate: refreshNowPlaying } = useNowPlaying(sessionId);

  const { data: session, error, mutate: refreshSession } = useSWR(['session', sessionId], () => sessionsApi.getSession(sessionId), {
    refreshInterval: 30000,
    shouldRetryOnError: false,
  });

  // Revalidates everything on the page, which was refused before the password was entered
  const onJoined = () => void mutate(() => true);

  // A scanned QR code arrives as ?join=<token>: unlock with it, then drop it from the address
  const [searchParams, setSearchParams] = useSearchParams();
  const joinToken = searchParams.get(JOIN_PARAM);
  const [joinFailed, setJoinFailed] = useState(false);

  useEffect(() => {
    if (!joinToken) return;

    sessionsApi
      .joinWithToken(sessionId, joinToken)
      .then(onJoined)
      .catch(() => setJoinFailed(true))
      .finally(() => {
        setSearchParams(
          (params) => {
            params.delete(JOIN_PARAM);
            return params;
          },
          { replace: true },
        );
      });
  }, [joinToken, sessionId]);

  const canViewSlideshow = isHost || isAdmin;

  const locked = (error as { status?: number } | undefined)?.status === HTTP_STATUS_CODE.UNAUTHORIZED;

  // Looked up even while the slideshow is shown, so the lyrics button can tell whether this song has any
  const lyrics = useLyrics(sessionId, nowPlaying?.song?.id, canViewSlideshow && Boolean(session) && !locked);
  const lyricsControls = {
    lyricsActive: showLyrics,
    lyricsAvailable: lyrics.status !== 'none',
    onToggleLyrics: () => setShowLyrics(!showLyrics),
  };

  const hostPlayerPush = useRef<HostPlayerPushHandlers>({});
  const hub = useSessionMusicHub(sessionId, Boolean(session && !locked), hostPlayerPush);

  return (
    <SessionHubContext.Provider value={hub}>
    <div className="w-full min-h-full bg-slate-800 text-slate-400 md:flex">
      {canViewSlideshow && session && !locked && slideshowOpen && !maximized && (
        <aside
          aria-label={showLyrics ? 'Lyrics' : 'Slideshow'}
          className="relative m-2 rounded-md border-2 border-slate-400 md:w-[calc(50%-1rem)] md:shrink-0"
        >
          <div className="aspect-video w-full overflow-hidden rounded-sm bg-black">
            {showLyrics ? (
              <LyricsView lyrics={lyrics} nowPlaying={nowPlaying} />
            ) : (
              <SlideshowView sessionId={session.id} />
            )}
          </div>
          <SlideshowControls
            open
            onToggle={() => setSlideshowOpen(false)}
            onMaximize={() => setMaximized(true)}
            {...lyricsControls}
            className="absolute top-1/2 right-2 -translate-y-1/2 md:-right-5"
          />
        </aside>
      )}
      {canViewSlideshow && session && !locked && !slideshowOpen && !maximized && (
        <SlideshowControls
          open={false}
          onToggle={() => setSlideshowOpen(true)}
          onMaximize={() => setMaximized(true)}
          {...lyricsControls}
          className="fixed top-1/2 left-1 z-10 -translate-y-1/2"
        />
      )}
      <div className="w-full max-w-xl mx-auto">
        <PageNavigation
          className="px-2 pt-4"
          links={[
            { to: ROUTE_PATHS.HOME, label: 'All sessions' },
            // Photos can be added only once the session is unlocked
            ...(session && !locked ? [{ onClick: uploadDialog.show, label: 'Upload photos' }] : []),
            ...(isAdmin ? [{ to: ROUTE_PATHS.ADMIN, label: 'Admin panel' }] : []),
          ]}
        />

        {locked && joinFailed && (
          <p className="px-2 pt-4 text-amber-500">This QR code has expired. Enter the password instead.</p>
        )}
        {locked && <SessionPasswordForm sessionId={sessionId} onJoined={onJoined} />}

        {error && !locked && (
          <p className="px-2 pt-4 text-amber-500">
            {(error as { status?: number }).status === HTTP_STATUS_CODE.NOT_FOUND
              ? 'This session does not exist (anymore).'
              : 'Could not load the session.'}{' '}
            <Link to={ROUTE_PATHS.HOME} className="underline">
              Back to the sessions
            </Link>
          </p>
        )}

        {session && !locked && (
          <>
            <SessionHeader
              session={session}
              isHost={isHost}
              isAdmin={isAdmin}
              onSessionChanged={() => void refreshSession()}
            />
            <NowPlayingCard nowPlaying={nowPlaying} />
            {/* An admin who is also the host plays the music themselves, so only the host's player is shown. */}
            {isHost && <HostPlayer sessionId={session.id} isAdmin={isAdmin} pushHandlers={hostPlayerPush} />}
            {!isHost && isAdmin && (
              <AdminPlayerControls
                sessionId={session.id}
                nowPlaying={nowPlaying}
                onChanged={() => void refreshNowPlaying()}
              />
            )}
            <Queue sessionId={session.id} canRemove={isHost || isAdmin} />
            <PhotoUploadDialog
              sessionId={session.id}
              open={uploadDialog.open}
              onClose={uploadDialog.close}
              onUnauthorized={() => {
                uploadDialog.close();
                onJoined();
              }}
            />
          </>
        )}
      </div>
      {canViewSlideshow && session && !locked && maximized && (
        <MaximizedView
          sessionId={session.id}
          nowPlaying={nowPlaying}
          showLyrics={showLyrics}
          lyrics={lyrics}
          onExit={() => setMaximized(false)}
        />
      )}
    </div>
    </SessionHubContext.Provider>
  );
};
