import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { ReactNode, useState } from 'react';
import { queueApi } from '@/service/endpoints/queue/queueApi';
import { useQueueSongs } from '@/service/endpoints/queue/useQueueSongs';

type Props = {
  sessionId: string;
  /** Host or admin: shows the delete icon on the queued songs. */
  canRemove?: boolean;
  /** Larger text for a screen at the party. */
  large?: boolean;
};

const errorMessage = (error: unknown) =>
  (error as { message?: string })?.message || 'Something went wrong';

/** The "Up next" list of a session's queue. */
export const QueueList = ({ sessionId, canRemove = false, large = false }: Props): ReactNode => {
  const [message, setMessage] = useState<string | null>(null);
  const { data: queue, mutate } = useQueueSongs(sessionId);

  // The server leaves the song that is playing out of the queue
  const upNext = queue ?? [];

  const removeSong = async (songId: number) => {
    try {
      await queueApi.remove(sessionId, songId);
      setMessage(null);
    } catch (error) {
      setMessage(errorMessage(error));
    }
    // Also after a failure: the song may be gone or already playing
    void mutate();
  };

  const headingClass = large
    ? 'px-2 pt-2 text-xl font-semibold text-slate-200'
    : 'text-xs font-semibold tracking-wider text-slate-400 uppercase';

  return (
    // The big screen shows the list on a plain background; on the session page it is a card like the rest
    <section aria-label="Up next" className={large ? '' : 'mx-2 mt-4 mb-4 p-3 bg-slate-900 rounded-lg'}>
      <h2 className={headingClass}>Up next</h2>
      {message && <p className="pt-2 text-sm text-amber-500">{message}</p>}
      {upNext.length === 0 ? (
        <p className={`text-sm text-slate-500 ${large ? 'px-2 pb-4' : 'pt-2'}`}>The queue is empty</p>
      ) : (
        <ol className={large ? 'px-2 pb-4 text-lg' : 'pt-1'}>
          {upNext.map((song, index) => {
            const label = song.artists ? `${song.artists} - ${song.name}` : song.name;
            return (
              <li
                key={song.id}
                className="flex items-center gap-3 py-2 border-b border-slate-700/60 last:border-b-0 hover:bg-slate-800/60"
              >
                <span className="w-6 shrink-0 text-right text-slate-500">{index + 1}.</span>
                <span className="min-w-0 flex-1 truncate" title={label}>
                  {label}
                </span>
                {song.isFallback && (
                  <span className="shrink-0 rounded-full bg-slate-700 px-2 py-0.5 text-xs text-slate-300">
                    from playlist
                  </span>
                )}
                {canRemove && (
                  <button
                    type="button"
                    aria-label={`Remove ${song.name} from the queue`}
                    onClick={() => void removeSong(song.id)}
                    className="shrink-0 rounded-md p-1 text-red-500 hover:bg-red-500/10 hover:text-red-400 cursor-pointer"
                  >
                    <DeleteOutlineIcon fontSize="small" />
                  </button>
                )}
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
};
