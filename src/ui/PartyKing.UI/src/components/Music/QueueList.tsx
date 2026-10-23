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

  return (
    <>
      <h2 className={`px-2 pt-2 font-semibold ${large ? 'text-xl text-slate-200' : ''}`}>Up next</h2>
      {message && <p className="px-2 pt-2 text-sm text-amber-500">{message}</p>}
      {upNext.length === 0 ? (
        <p className="px-2 pb-4 text-sm text-slate-500">The queue is empty</p>
      ) : (
        <ol className={`px-2 pb-4 ${large ? 'text-lg' : ''}`}>
          {upNext.map((song, index) => {
            const label = song.artists ? `${song.artists} - ${song.name}` : song.name;
            return (
              <li
                key={song.id}
                className="flex items-center gap-3 border-b border-slate-700/60 py-1 last:border-b-0 hover:bg-slate-800/60"
              >
                <span className="w-6 shrink-0 text-right text-slate-500">{index + 1}.</span>
                <span className="min-w-0 flex-1 truncate" title={label}>
                  {label}
                </span>
                {canRemove && (
                  <button
                    type="button"
                    aria-label={`Remove ${song.name} from the queue`}
                    onClick={() => void removeSong(song.id)}
                    className="shrink-0 rounded p-1 text-red-500 hover:bg-red-500/10 hover:text-red-400 cursor-pointer"
                  >
                    <DeleteOutlineIcon fontSize="small" />
                  </button>
                )}
              </li>
            );
          })}
        </ol>
      )}
    </>
  );
};
