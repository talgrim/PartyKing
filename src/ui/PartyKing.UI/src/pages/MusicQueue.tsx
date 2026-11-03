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
import { useAutoLyrics } from '@/components/Music/useAutoLyrics';
import { useUploadDialog } from '@/components/Music/useUploadDialog';
import { SessionPasswordForm } from '@/components/Music/SessionPasswordForm';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ResistanceInvite } from '@/pages/Games/ResistanceInvite';
import { RingoInvite } from '@/pages/Games/RingoInvite';
import { SlideshowView } from '@/pages/Slideshow/SlideshowView';
import { ROUTE_PATHS } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { lyricsApi } from '@/service/endpoints/lyrics/lyricsApi';
import { LyricsDto } from '@/service/endpoints/lyrics/types';
import { useLyrics } from '@/service/endpoints/lyrics/useLyrics';
import { useNowPlaying } from '@/service/endpoints/player/useNowPlaying';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';
import {
  applyLyricsOffsetChanged,
  HostPlayerPushHandlers,
  lyricsKey,
} from '@/service/realtime/sessionMusicEvents';
import { useSessionMusicHub } from '@/service/realtime/useSessionMusicHub';
import { SessionHubContext } from '@/service/realtime/SessionHubContext';
import { isMobileDevice } from '@/utils/isMobileDevice';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { useTranslation } from 'react-i18next';

const JOIN_PARAM = 'join';

/**
 * Page of a session, behind the session password. Anyone who entered it (admins included) sees what is playing and
 * can search for or paste songs to add to the queue, no login needed. The host also gets the player, and an admin
 * the controls to pause and skip. Everyone gets a drawer on the left with the lyrics of the song playing, which
 * can be hidden (hidden by default on a phone or tablet) or maximized; the host and admins can also switch it to the slideshow.
 */
export const MusicQueue = (): ReactNode => {
  const { t } = useTranslation();
  const { sessionId = '' } = useParams();

  const { mutate } = useSWRConfig();
  const [slideshowOpen, setSlideshowOpen] = usePersistedFlag('partyking.slideshowOpen', !isMobileDevice());
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
    // Runs once per token: onJoined and setSearchParams are new functions on every render
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [joinToken, sessionId]);

  // Guests get the lyrics only: the slideshow is for the host and admins, so no guest ever requests the photos
  const canViewSlideshow = isHost || isAdmin;

  const locked = (error as { status?: number } | undefined)?.status === HTTP_STATUS_CODE.UNAUTHORIZED;

  // Looked up even while the slideshow is shown, so the lyrics button can tell whether this song has any
  const lyrics = useLyrics(sessionId, nowPlaying?.song?.id, Boolean(session) && !locked);

  // With the automatic mode on, each song shows its lyrics once they are found and the slideshow while they are
  // searched for or missing. The toggle still works; the next result overrides it.
  const [autoLyrics] = useAutoLyrics();
  useEffect(() => {
    if (!canViewSlideshow || !autoLyrics || (lyrics.status !== 'found' && lyrics.status !== 'none')) return;
    setShowLyrics(lyrics.status === 'found');
    // Only a new result matters, not a change of the setter's identity
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoLyrics, lyrics.status, nowPlaying?.song?.id]);

  // Host and admins only (the server refuses everyone else); the push reaches the others, and this viewer is
  // updated at once rather than when its own push arrives
  const playingSongId = nowPlaying?.song?.id;
  const saveLyricsOffset = async (offsetMs: number) => {
    if (playingSongId == null) return;
    await lyricsApi.setOffset(sessionId, offsetMs);
    await mutate(
      lyricsKey(sessionId, playingSongId),
      (current: LyricsDto | null | undefined) => applyLyricsOffsetChanged(current, offsetMs),
      { revalidate: false },
    );
  };

  const lyricsOnly = !canViewSlideshow;
  const lyricsShown = lyricsOnly || showLyrics;

  const lyricsControls = {
    lyricsOnly,
    lyricsActive: lyricsShown,
    lyricsAvailable: lyrics.status !== 'none',
    onToggleLyrics: () => setShowLyrics(!showLyrics),
  };

  const hostPlayerPush = useRef<HostPlayerPushHandlers>({});
  const hub = useSessionMusicHub(sessionId, Boolean(session && !locked), hostPlayerPush);
  const unlocked = Boolean(session && !locked);
  // The admin opens the games, so everyone else (the host included) is invited to join
  const invitable = unlocked && !isAdmin;

  return (
    <SessionHubContext.Provider value={hub}>
    <div className="w-full min-h-full bg-slate-800 text-slate-400 md:flex">
      {session && !locked && slideshowOpen && !maximized && (
        <aside
          aria-label={lyricsShown ? t('views.lyrics') : t('views.slideshow')}
          className="relative mx-2 mb-2 mt-9 rounded-md border-2 border-slate-400 md:w-[calc(50%-1rem)] md:shrink-0"
        >
          <div className="aspect-video w-full overflow-hidden rounded-sm bg-black">
            {lyricsShown ? (
              <LyricsView
                lyrics={lyrics}
                nowPlaying={nowPlaying}
                onResync={() => void refreshNowPlaying()}
                onSaveOffset={canViewSlideshow ? saveLyricsOffset : undefined}
              />
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
      {session && !locked && !slideshowOpen && !maximized && (
        <SlideshowControls
          open={false}
          onToggle={() => setSlideshowOpen(true)}
          onMaximize={() => setMaximized(true)}
          {...lyricsControls}
          className="fixed top-1/2 left-1 z-10 -translate-y-1/2"
        />
      )}
      <div className="w-full max-w-xl mx-auto pt-6">
        <PageNavigation
          className="px-2 pt-4"
          links={[
            { to: ROUTE_PATHS.HOME, label: t('admin.allSessions') },
            // Photos can be added only once the session is unlocked
            ...(session && !locked ? [{ onClick: uploadDialog.show, label: t('photoUpload.title') }] : []),
            ...(isAdmin ? [{ to: ROUTE_PATHS.ADMIN, label: t('home.adminPanel') }] : []),
          ]}
        />

        {locked && joinFailed && (
          <p className="px-2 pt-4 text-amber-500">{t('sessionPage.qrExpired')}</p>
        )}
        {invitable && <RingoInvite sessionId={sessionId} enabled />}
        {invitable && <ResistanceInvite sessionId={sessionId} enabled />}
        {locked && <SessionPasswordForm sessionId={sessionId} onJoined={onJoined} />}

        {error && !locked && (
          <p className="px-2 pt-4 text-amber-500">
            {(error as { status?: number }).status === HTTP_STATUS_CODE.NOT_FOUND
              ? t('sessionPage.notFound')
              : t('sessionPage.loadFailed')}{' '}
            <Link to={ROUTE_PATHS.HOME} className="underline">
              {t('common.backToSessions')}
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
      {session && !locked && maximized && (
        <MaximizedView
          sessionId={session.id}
          nowPlaying={nowPlaying}
          showLyrics={lyricsShown}
          lyrics={lyrics}
          onResyncLyrics={() => void refreshNowPlaying()}
          onSaveLyricsOffset={canViewSlideshow ? saveLyricsOffset : undefined}
          onExit={() => setMaximized(false)}
        />
      )}
    </div>
    </SessionHubContext.Provider>
  );
};
