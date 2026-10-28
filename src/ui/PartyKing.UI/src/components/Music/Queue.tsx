import CloseIcon from '@mui/icons-material/Close';
import { ChangeEvent, FormEvent, ReactNode, useEffect, useRef, useState } from 'react';
import { QueueList } from '@/components/Music/QueueList';
import { looksLikeSongLink, queueApi } from '@/service/endpoints/queue/queueApi';
import { useQueueSongs } from '@/service/endpoints/queue/useQueueSongs';
import { SpotifyTrackDto } from '@/service/endpoints/queue/types';

type Props = {
  sessionId: string;
  /** Host or admin: shows the delete icon on the queued songs. */
  canRemove?: boolean;
};

type RowState = { status: 'adding' } | { status: 'failed'; error: string };

const errorMessage = (error: unknown) =>
  (error as { message?: string })?.message || 'Something went wrong';

export const Queue = ({ sessionId, canRemove = false }: Props): ReactNode => {
  const [input, setInput] = useState('');
  const [results, setResults] = useState<SpotifyTrackDto[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [rows, setRows] = useState<Record<string, RowState>>({});
  const [open, setOpen] = useState(false);
  const searchRef = useRef<HTMLElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const { data: queue, mutate } = useQueueSongs(sessionId);

  // The server leaves the song that is playing out of the queue
  const upNext = queue ?? [];
  // A Spotify queue item stores the track uri as its url, so this marks tracks that are still waiting in the queue.
  const queuedUrls = new Set(upNext.map((song) => song.url));

  // The results dropdown closes on a tap anywhere outside the search area, or on Escape. The results are kept, so
  // focusing the search field opens them again.
  useEffect(() => {
    if (!open) return;
    const closeOnOutsidePress = (e: PointerEvent) => {
      if (!searchRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const closeOnEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('pointerdown', closeOnOutsidePress);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePress);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [open]);

  const clearSearch = () => {
    setInput('');
    setResults([]);
    setRows({});
    setMessage(null);
    setOpen(false);
    inputRef.current?.focus();
  };

  const setRow = (uri: string, state: RowState | null) =>
    setRows((current) => {
      const { [uri]: _removed, ...rest } = current;
      return state ? { ...rest, [uri]: state } : rest;
    });

  const enqueue = async (value: string) => {
    setBusy(true);
    try {
      await queueApi.enqueue(sessionId, value);
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

  // Adds a search result without closing the list, so several songs can be picked from one search. The row stays
  // locked until the queue is refreshed, which is when it starts showing as "In queue".
  const enqueueResult = async (uri: string) => {
    setRow(uri, { status: 'adding' });
    try {
      await queueApi.enqueue(sessionId, uri);
      await mutate();
      setRow(uri, null);
    } catch (error) {
      setRow(uri, { status: 'failed', error: errorMessage(error) });
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
      const tracks = await queueApi.searchSpotify(sessionId, value);
      setResults(tracks);
      setRows({});
      setOpen(tracks.length > 0);
      setMessage(tracks.length === 0 ? 'No songs found' : null);
    } catch (error) {
      setMessage(errorMessage(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <section ref={searchRef} aria-label="Add a song" className="relative mx-2 mt-4 p-3 bg-slate-900 rounded-lg">
        <h2 className="pb-3 text-xs font-semibold tracking-wider text-slate-400 uppercase">
          Add a song to the queue
        </h2>
        <form className="flex" onSubmit={handleSubmit}>
          <div className="relative grow">
            <input
              ref={inputRef}
              type="text"
              value={input}
              onChange={(e: ChangeEvent<HTMLInputElement>) => setInput(e.target.value)}
              onFocus={() => setOpen(results.length > 0)}
              onClick={() => setOpen(results.length > 0)}
              className="w-full pl-3 pr-10 py-2 bg-slate-800 border border-slate-700 rounded-l-md placeholder:text-slate-500 focus:outline-none focus:border-amber-600"
              placeholder="Search Spotify, or paste a Spotify / YouTube link"
            />
            {input && (
              <button
                type="button"
                onClick={clearSearch}
                aria-label="Clear search"
                className="absolute inset-y-0 right-0 w-10 flex items-center justify-center text-slate-500 hover:text-slate-200 cursor-pointer"
              >
                <CloseIcon fontSize="small" />
              </button>
            )}
          </div>
          <button
            type="submit"
            disabled={busy}
            className="w-20 font-semibold text-slate-100 bg-amber-600 rounded-r-md cursor-pointer hover:bg-amber-500 disabled:opacity-50"
          >
            {looksLikeSongLink(input) ? 'Add' : 'Search'}
          </button>
        </form>

        {message && <p className="pt-2 text-sm text-amber-500">{message}</p>}

        {open && results.length > 0 && (
          <ul className="absolute left-0 right-0 top-full z-10 mt-1 max-h-[60vh] overflow-y-auto p-2 space-y-2 rounded-md border border-slate-700 bg-slate-800 shadow-lg">
            {results.map((track) => {
              const row = rows[track.uri];
              const inQueue = queuedUrls.has(track.uri);
              const adding = row?.status === 'adding';
              return (
                <li
                  key={track.uri}
                  className={`p-2 rounded-md border-l-4 ${
                    inQueue ? 'bg-slate-900/50 border-slate-600' : 'bg-slate-900 border-amber-600 hover:bg-slate-700'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    {track.albumArtUrl ? (
                      <img src={track.albumArtUrl} alt="" className="w-12 h-12 rounded-sm shrink-0" />
                    ) : (
                      <div className="w-12 h-12 rounded-sm shrink-0 bg-slate-700" />
                    )}
                    <div className="grow min-w-0">
                      <div className={`truncate font-medium ${inQueue ? 'text-slate-500' : 'text-slate-200'}`}>
                        {track.name}
                      </div>
                      <div className="truncate text-sm text-slate-500">{track.artists}</div>
                    </div>
                    <button
                      disabled={adding || inQueue}
                      onClick={() => enqueueResult(track.uri)}
                      className="w-24 shrink-0 px-2 py-2 bg-amber-600 text-slate-100 font-semibold rounded-md cursor-pointer hover:bg-amber-500 disabled:cursor-default disabled:bg-slate-700 disabled:text-slate-400 disabled:hover:bg-slate-700"
                    >
                      {inQueue ? 'In queue' : adding ? 'Adding...' : 'Add'}
                    </button>
                  </div>
                  {row?.status === 'failed' && <p className="pt-1 text-sm text-amber-500">{row.error}</p>}
                </li>
              );
            })}
          </ul>
        )}
      </section>

      <QueueList sessionId={sessionId} canRemove={canRemove} />
    </>
  );
};
