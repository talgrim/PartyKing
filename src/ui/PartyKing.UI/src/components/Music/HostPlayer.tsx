import { ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import useSWR from 'swr';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { playerApi } from '@/service/endpoints/player/playerApi';
import { useNowPlaying } from '@/service/endpoints/player/useNowPlaying';
import { queueApi } from '@/service/endpoints/queue/queueApi';
import { SongDto, SongSource } from '@/service/endpoints/queue/types';
import { ErrorLog, ErrorLogEntry } from './ErrorLog';
import { PlayerControls } from './PlayerControls';
import { SpotifyPlayer, SpotifyPlayerHandle } from './SpotifyPlayer';
import { YoutubePlayer } from './YoutubePlayer';
import { describeYoutubeError } from './youtubeErrors';

const MAX_LOGGED_ERRORS = 50;
// After this many YouTube failures in a row something is wrong beyond a single video, so stop skipping.
const MAX_CONSECUTIVE_YOUTUBE_FAILURES = 2;
const TOKEN_REFRESH_MS = 30 * 60 * 1000;
// The server treats a player that has not reported for ~10 s as gone, so report well within that.
const STATE_REPORT_INTERVAL_MS = 2000;
// A host login that comes back to this page and is still rejected within this window is a loop.
const LOGIN_LOOP_WINDOW_MS = 30 * 1000;

const loginAttemptKey = (sessionId: string) => `host-login-attempt:${sessionId}`;

type Props = {
  sessionId: string;
  /** Only the admin sees the error log. */
  isAdmin: boolean;
};

/**
 * The player of a session. It plays the music in this browser tab, so it is only shown to the session's host, who
 * must keep the page open. The server decides which song is current: a song an admin skipped to is picked up from
 * there, and the player reports its pause state back so everyone can see it.
 */
export const HostPlayer = ({ sessionId, isAdmin }: Props): ReactNode => {
  const spotifyPlayer = useRef<SpotifyPlayerHandle>(null);
  const advancing = useRef(false);
  const failedSongId = useRef<number | null>(null);
  const consecutiveYoutubeFailures = useRef(0);
  const nextLogId = useRef(0);

  const [started, setStarted] = useState(false);
  const [song, setSong] = useState<SongDto | null>(null);
  const [paused, setPaused] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorLog, setErrorLog] = useState<ErrorLogEntry[]>([]);

  // Callbacks that fire at arbitrary times (timers, the Spotify SDK) read the latest state through these.
  const songRef = useRef(song);
  const pausedRef = useRef(paused);
  songRef.current = song;
  pausedRef.current = paused;

  const { data: nowPlaying, mutate: refreshNowPlaying } = useNowPlaying(sessionId);

  // Only the admin sees the log; everyone who can see the player sees the latest message.
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
      reportError('Could not start the host login');
    }
  }, [sessionId, reportError]);

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

  // Tells the server which song this player thinks is playing, so it only advances when that is still true.
  // When it is not (an admin skipped meanwhile), the song really playing comes back and nothing advances.
  const playNext = useCallback(async () => {
    if (advancing.current) return;
    advancing.current = true;
    try {
      setSong(await queueApi.next(sessionId, songRef.current?.id));
      setPaused(false);
      setError(null);
      void refreshNowPlaying();
    } catch (e) {
      const status = (e as { status?: number })?.status;
      if (status === HTTP_STATUS_CODE.NOT_FOUND) {
        setSong(null);
        setPaused(false);
        void refreshNowPlaying();
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
  }, [sessionId, loginAsHost, reportError, refreshNowPlaying]);

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

  // Plays whatever the server says is playing (after a reload that is the song that was interrupted), or else
  // the next song in the queue.
  const start = () => {
    // The click is the user gesture that lets the browser play audio.
    spotifyPlayer.current?.activate();
    setStarted(true);
    void playNext();
  };

  // Spotify reports its state back through onPausedChange, so only ask it to toggle. YouTube is driven by state.
  const togglePlay = useCallback(() => {
    if (songRef.current?.source === SongSource.Spotify) {
      spotifyPlayer.current?.togglePlay();
    } else {
      setPaused((current) => !current);
    }
  }, []);

  // Follow the server when it moves on without us, i.e. an admin skipped the song.
  const serverSong = nowPlaying?.song ?? null;
  const serverSongId = serverSong?.id ?? null;
  useEffect(() => {
    if (!started || !nowPlaying || (songRef.current?.id ?? null) === serverSongId) return;

    if (serverSong) {
      consecutiveYoutubeFailures.current = 0;
      setSong(serverSong);
      setPaused(false);
      setError(null);
    } else {
      // The skipped song was the last one; a Spotify track would otherwise play on
      if (songRef.current?.source === SongSource.Spotify && !pausedRef.current) {
        spotifyPlayer.current?.togglePlay();
      }
      setSong(null);
      setPaused(false);
    }
    // Only a change of the server's song matters, not every poll.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [started, serverSongId]);

  // Tell the server what we are doing: that is how guests see the pause state and how it knows we are connected.
  // An admin's pause or resume comes back as the answer.
  const reportState = useCallback(async () => {
    try {
      const { command } = await playerApi.reportState(sessionId, {
        songId: songRef.current?.id ?? null,
        paused: pausedRef.current,
      });
      const shouldToggle =
        songRef.current !== null &&
        ((command === 'Pause' && !pausedRef.current) || (command === 'Resume' && pausedRef.current));
      if (shouldToggle) togglePlay();
    } catch {
      // The next report tries again; meanwhile the server shows the player as not connected
    }
  }, [sessionId, togglePlay]);

  // A change of song or pause state is reported right away rather than at the next tick.
  useEffect(() => {
    if (!started) return;
    void reportState();
    const timer = setInterval(() => void reportState(), STATE_REPORT_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [started, reportState, song?.id, paused]);

  // Nothing is playing: pick up songs as soon as somebody queues them.
  const hasQueuedSongs = (queue ?? []).length > 0;
  useEffect(() => {
    if (started && !song && hasQueuedSongs) {
      void playNext();
    }
  }, [started, song, hasQueuedSongs, playNext]);

  return (
    <>
      <div className="px-2 pt-4">
        {!started && (
          <div className="text-center">
            <button
              onClick={start}
              disabled={!token}
              className="p-4 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50"
            >
              Start the music
            </button>
            <p className="pt-2 text-sm text-slate-500">The music plays in this tab, so keep the page open.</p>
          </div>
        )}

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

      <PlayerControls
        paused={paused}
        disabled={!started}
        hasSong={song !== null}
        onToggle={togglePlay}
        onNext={playNextManually}
      />

      {isAdmin && (
        <div className="pt-4">
          <ErrorLog entries={errorLog} />
        </div>
      )}
    </>
  );
};
