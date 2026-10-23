import { ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import useSWR from 'swr';
import { Queue } from '@/components/Music/Queue';
import { SpotifyPlayer, SpotifyPlayerHandle } from '@/components/Music/SpotifyPlayer';
import { YoutubePlayer } from '@/components/Music/YoutubePlayer';
import { ROUTE_PATHS } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { queueApi } from '@/service/endpoints/queue/queueApi';
import { SongDto, SongSource } from '@/service/endpoints/queue/types';

const TOKEN_REFRESH_MS = 30 * 60 * 1000;

export const Music = (): ReactNode => {
  const navigate = useNavigate();
  const spotifyPlayer = useRef<SpotifyPlayerHandle>(null);
  const advancing = useRef(false);

  const [started, setStarted] = useState(false);
  const [song, setSong] = useState<SongDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Only the logged-in host plays the music. This also tells us whether we need to log in.
  const { data: token } = useSWR('spotify-token', queueApi.getSpotifyToken, {
    refreshInterval: TOKEN_REFRESH_MS,
    revalidateOnFocus: false,
    shouldRetryOnError: false,
    onError: (e) => {
      if (e?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        navigate(ROUTE_PATHS.LOGIN, { replace: true });
      }
    },
  });

  const { data: queue } = useSWR('queue-songs', queueApi.getQueue, { refreshInterval: 3000 });

  const playNext = useCallback(async () => {
    if (advancing.current) return;
    advancing.current = true;
    try {
      setSong(await queueApi.next());
      setError(null);
    } catch (e) {
      const status = (e as { status?: number })?.status;
      if (status === HTTP_STATUS_CODE.NOT_FOUND) {
        setSong(null);
      } else if (status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        navigate(ROUTE_PATHS.LOGIN, { replace: true });
      } else {
        setError('Could not load the next song');
      }
    } finally {
      advancing.current = false;
    }
  }, [navigate]);

  const start = () => {
    // The click is the user gesture that lets the browser play audio.
    spotifyPlayer.current?.activate();
    setStarted(true);
    void playNext();
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
      <div className="col-span-7 place-content-center">
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
          <YoutubePlayer key={song.id} songUrl={song.url} mute={false} onEnded={playNext} />
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
            onError={setError}
          />
        )}

        {error && <p className="mt-4 text-center text-amber-500">{error}</p>}
      </div>

      <div className="col-span-5">
        <Queue currentSongId={song?.id} />
        <div className="space-x-4 px-2">
          <button
            onClick={playNext}
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
