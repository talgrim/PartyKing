import { ReactNode } from 'react';
import { NowPlayingDto } from '@/service/endpoints/player/types';

type Props = {
  nowPlaying: NowPlayingDto | undefined;
};

/** The song playing right now: album art (or the video thumbnail), title and artist. Visible to everyone. */
export const NowPlayingCard = ({ nowPlaying }: Props): ReactNode => {
  if (!nowPlaying) return null;

  const { song, paused, playerConnected } = nowPlaying;

  return (
    <section aria-label="Now playing" className="mx-2 mt-4 p-3 bg-slate-900 rounded-lg">
      <h2 className="text-xs font-semibold tracking-wider text-amber-500 uppercase">Now playing</h2>

      {!song ? (
        <p className="pt-2 text-sm text-slate-500">Nothing is playing. Add a song to the queue.</p>
      ) : (
        <div className="flex items-center gap-4 pt-3">
          {song.albumArtUrl ? (
            <img src={song.albumArtUrl} alt="" className="w-24 h-24 rounded-md object-cover shrink-0 shadow-lg" />
          ) : (
            <div className="w-24 h-24 rounded-md bg-slate-700 shrink-0" />
          )}
          <div className="min-w-0">
            <div className="text-xl font-semibold text-slate-100 truncate">{song.name}</div>
            {song.artists && <div className="truncate">{song.artists}</div>}
            {paused && (
              <span className="inline-block mt-1 px-2 py-0.5 text-xs text-amber-500 bg-slate-800 rounded-full">
                Paused
              </span>
            )}
            {!playerConnected && <p className="mt-1 text-sm text-slate-500">The host's player is not connected.</p>}
          </div>
        </div>
      )}
    </section>
  );
};
