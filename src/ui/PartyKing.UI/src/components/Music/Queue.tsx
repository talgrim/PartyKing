import { ChangeEvent, FormEvent, ReactNode, useState } from 'react';
import useSWR from 'swr';
import { looksLikeSongLink, queueApi } from '@/service/endpoints/queue/queueApi';
import { SongDto, SpotifyTrackDto } from '@/service/endpoints/queue/types';

type Props = {
  /** Id of the song that is playing right now; it is shown separately from the "up next" list. */
  currentSongId?: number;
};

const errorMessage = (error: unknown) =>
  (error as { title?: string; message?: string })?.message ||
  (error as { title?: string })?.title ||
  'Something went wrong';

export const Queue = ({ currentSongId }: Props): ReactNode => {
  const [input, setInput] = useState('');
  const [results, setResults] = useState<SpotifyTrackDto[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const { data: queue, mutate } = useSWR<SongDto[]>('queue-songs', queueApi.getQueue, {
    refreshInterval: 3000,
  });

  const upNext = (queue ?? []).filter((song) => song.id !== currentSongId);

  const enqueue = async (value: string) => {
    setBusy(true);
    try {
      await queueApi.enqueue(value);
      setMessage('Added to the queue');
      setInput('');
      setResults([]);
      void mutate();
    } catch (error) {
      setMessage(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const value = input.trim();
    if (!value) return;

    if (looksLikeSongLink(value)) {
      await enqueue(value);
      return;
    }

    setBusy(true);
    try {
      const tracks = await queueApi.searchSpotify(value);
      setResults(tracks);
      setMessage(tracks.length === 0 ? 'No songs found' : null);
    } catch (error) {
      setMessage(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <form className="flex mt-4 mb-2 px-2" onSubmit={handleSubmit}>
        <input
          type="text"
          value={input}
          onChange={(e: ChangeEvent<HTMLInputElement>) => setInput(e.target.value)}
          className="w-full px-2 py-2 border-1 border-gray-700 rounded-sm focus:outline-none"
          placeholder="Search Spotify, or paste a Spotify / YouTube link"
        />
        <button type="submit" disabled={busy} className="bg-slate-900 w-20 cursor-pointer disabled:opacity-50">
          {looksLikeSongLink(input) ? 'Add' : 'Search'}
        </button>
      </form>

      {message && <p className="px-2 pb-2 text-sm text-amber-500">{message}</p>}

      {results.length > 0 && (
        <ul className="px-2 pb-4 space-y-1">
          {results.map((track) => (
            <li key={track.uri} className="flex items-center gap-2">
              {track.albumArtUrl && <img src={track.albumArtUrl} alt="" className="w-10 h-10 rounded-sm" />}
              <div className="grow min-w-0">
                <div className="truncate">{track.name}</div>
                <div className="truncate text-sm text-slate-500">{track.artists}</div>
              </div>
              <button
                disabled={busy}
                onClick={() => enqueue(track.uri)}
                className="px-2 py-1 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50"
              >
                Add
              </button>
            </li>
          ))}
        </ul>
      )}

      <h2 className="px-2 pt-2 font-semibold">Up next</h2>
      {upNext.length === 0 ? (
        <p className="px-2 pb-4 text-sm text-slate-500">The queue is empty</p>
      ) : (
        <ol className="px-2 pb-4 list-decimal list-inside">
          {upNext.map((song) => (
            <li key={song.id} className="truncate">
              {song.artists ? `${song.artists} - ${song.name}` : song.name}
            </li>
          ))}
        </ol>
      )}
    </>
  );
};
