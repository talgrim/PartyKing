import { ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import useSWR from 'swr';
import { ErrorLog, ErrorLogEntry } from '@/components/Music/ErrorLog';
import { PlayPauseButton } from '@/components/Music/PlayPauseButton';
import { Queue } from '@/components/Music/Queue';
import { SpotifyPlayer, SpotifyPlayerHandle } from '@/components/Music/SpotifyPlayer';
import { YoutubePlayer } from '@/components/Music/YoutubePlayer';
import { describeYoutubeError } from '@/components/Music/youtubeErrors';
import { sessionPath } from '@/router/constants';
import { useIsAdmin } from '@/service/endpoints/admin/useIsAdmin';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { queueApi } from '@/service/endpoints/queue/queueApi';
import { SongDto, SongSource } from '@/service/endpoints/queue/types';

const MAX_LOGGED_ERRORS = 50;
// After this many YouTube failures in a row something is wrong beyond a single video, so stop skipping.
const MAX_CONSECUTIVE_YOUTUBE_FAILURES = 2;
const TOKEN_REFRESH_MS = 30 * 60 * 1000;
// A host login that comes back to this page and is still rejected within this window is a loop.
const LOGIN_LOOP_WINDOW_MS = 30 * 1000;

const loginAttemptKey = (sessionId: string) => `host-login-attempt:${sessionId}`;

export const Music = (): ReactNode => {
  const navigate = useNavigate();
  const { sessionId = '' } = useParams();
  const spotifyPlayer = useRef<SpotifyPlayerHandle>(null);
  const advancing = useRef(false);
  const failedSongId = useRef<number | null>(null);
  const consecutiveYoutubeFailures = useRef(0);
  const nextLogId = useRef(0);
  const isAdmin = useIsAdmin();

  const [started, setStarted] = useState(false);
  const [song, setSong] = useState<SongDto | null>(null);
  const [paused, setPaused] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorLog, setErrorLog] = useState<ErrorLogEntry[]>([]);

  // Only the admin sees the log; everyone on this page sees the latest message.
  const logError = useCallback((message: string) => {
    const entry = { id: nextLogId.current++, time: new Date(), message };
    setErrorLog((entries) => [...entries, entry].slice(-MAX_LOGGED_ERRORS));
  }, []);

  const reportError = useCallback(
    (message: string) => {
      setError(message);
      logError(message);
    },
    [logError],
  );

  // Not logged in: go through Spotify again. The login hands the session back to the same host,
  // and rejects anyone else.
  const loginAsHost = useCallback(async () => {
    const lastAttempt = Number(sessionStorage.getItem(loginAttemptKey(sessionId)));
    if (Date.now() - lastAttempt < LOGIN_LOOP_WINDOW_MS) {
      // We just came back from Spotify and are still not logged in (e.g. the cookie was not stored).
      // Redirecting again would loop forever.
      reportError('Host login did not stick. Check that cookies are enabled and the page and API use the same host.');
      return;
    }
    try {
      sessionStorage.setItem(loginAttemptKey(sessionId), String(Date.now()));
      window.location.href = await sessionsApi.createHostLoginUrl(sessionId);
    } catch {
      navigate(sessionPath(sessionId), { replace: true });
    }
  }, [sessionId, navigate, reportError]);

  // Only the logged-in host plays the music. This also tells us whether we need to log in.
  const { data: token } = useSWR('spotify-token', queueApi.getSpotifyToken, {
    refreshInterval: TOKEN_REFRESH_MS,
    revalidateOnFocus: false,
    shouldRetryOnError: false,
    onError: (e) => {
      if (e?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        void loginAsHost();
      }
    },
  });

  // Logged in for real: the next 401 is a new login, not a loop.
  useEffect(() => {
    if (token) sessionStorage.removeItem(loginAttemptKey(sessionId));
  }, [token, sessionId]);

  const { data: queue } = useSWR(['queue-songs', sessionId], () => queueApi.getQueue(sessionId), {
    refreshInterval: 3000,
  });

  const playNext = useCallback(async () => {
    if (advancing.current) return;
    advancing.current = true;
    try {
      setSong(await queueApi.next(sessionId));
      setPaused(false);
      setError(null);
    } catch (e) {
      const status = (e as { status?: number })?.status;
      if (status === HTTP_STATUS_CODE.NOT_FOUND) {
        setSong(null);
        setPaused(false);
      } else if (status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        void loginAsHost();
      } else if (status === HTTP_STATUS_CODE.FORBIDDEN) {
        reportError('Only the host of this session can play its music');
      } else {
        reportError('Could not load the next song');
      }
    } finally {
      advancing.current = false;
    }
  }, [sessionId, loginAsHost, reportError]);

  // YouTube refuses some videos (e.g. embedding disabled by the owner): say why and move on.
  const skipFailedYoutubeSong = async (failed: SongDto, code: number | undefined) => {
    if (failedSongId.current === failed.id) return;
    failedSongId.current = failed.id;

    const reason = describeYoutubeError(code);
    logError(`YouTube error ${code ?? 'unknown'} on "${failed.name}" (${failed.url}): ${reason}`);

    consecutiveYoutubeFailures.current += 1;
    if (consecutiveYoutubeFailures.current >= MAX_CONSECUTIVE_YOUTUBE_FAILURES) {
      setError(
        `YouTube refused ${consecutiveYoutubeFailures.current} videos in a row (${reason}). ` +
          'Not skipping any more songs; press "Next song" to continue.',
      );
      return;
    }

    await playNext();
    setError(`Skipped "${failed.name}": ${reason}`);
  };

  // Playback that really starts ends a run of failures.
  const handlePausedChange = (isPaused: boolean) => {
    if (!isPaused) consecutiveYoutubeFailures.current = 0;
    setPaused(isPaused);
  };

  const playNextManually = () => {
    consecutiveYoutubeFailures.current = 0;
    void playNext();
  };

  const start = () => {
    // The click is the user gesture that lets the browser play audio.
    spotifyPlayer.current?.activate();
    setStarted(true);
    void playNext();
  };

  // Spotify reports its state back through onPausedChange, so only ask it to toggle. YouTube is driven by state.
  const togglePlay = () => {
    if (song?.source === SongSource.Spotify) {
      spotifyPlayer.current?.togglePlay();
    } else {
      setPaused((current) => !current);
    }
  };

  // Nothing is playing: pick up songs as soon as somebody queues them.
  const hasQueuedSongs = (queue ?? []).length > 0;
  useEffect(() => {
    if (started && !song && hasQueuedSongs) {
      void playNext();
    }
  }, [started, song, hasQueuedSongs, playNext]);

  return (
    <div className="w-full h-full grid grid-cols-12 bg-slate-800 text-slate-400">
      <div className="col-span-7 flex flex-col min-h-0">
        <div className="grow place-content-center">
          {!started && (
            <div className="text-center">
              <button
                onClick={start}
                disabled={!token}
                className="p-4 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50"
              >
                Start the music
              </button>
            </div>
          )}

          {started && !song && <p className="text-center">Waiting for songs. Add one to the queue.</p>}

          {song?.source === SongSource.Youtube && (
            <YoutubePlayer
              key={song.id}
              songUrl={song.url}
              mute={false}
              paused={paused}
              onPausedChange={handlePausedChange}
              onEnded={playNext}
              onError={(code) => void skipFailedYoutubeSong(song, code)}
            />
          )}

          {song?.source === SongSource.Spotify && (
            <div className="flex flex-col items-center gap-4">
              {song.albumArtUrl && <img src={song.albumArtUrl} alt="" className="w-80 h-80 rounded-md" />}
              <div className="text-2xl text-slate-200">{song.name}</div>
              <div>{song.artists}</div>
            </div>
          )}

          {/* Always mounted so the Spotify device is ready before the first Spotify song. */}
          {token && (
            <SpotifyPlayer
              handle={spotifyPlayer}
              uri={song?.source === SongSource.Spotify ? song.url : null}
              onEnded={playNext}
              onPausedChange={handlePausedChange}
              onError={reportError}
            />
          )}

          {error && <p className="mt-4 text-center text-amber-500">{error}</p>}
        </div>

        {isAdmin && <ErrorLog entries={errorLog} />}
      </div>

      <div className="col-span-5">
        <Queue sessionId={sessionId} currentSongId={song?.id} />
        <div className="space-x-4 px-2">
          <PlayPauseButton paused={paused} disabled={!started || !song} onToggle={togglePlay} />
          <button
            onClick={playNextManually}
            disabled={!started}
            className="p-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50"
          >
            Next song
          </button>
        </div>
      </div>
    </div>
  );
};
