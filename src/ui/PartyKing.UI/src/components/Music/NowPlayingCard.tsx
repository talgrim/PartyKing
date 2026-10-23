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
    <section aria-label="Now playing" className="px-2 pt-4">
      <h2 className="font-semibold">Now playing</h2>

      {!song ? (
        <p className="text-sm text-slate-500">Nothing is playing. Add a song to the queue.</p>
      ) : (
        <div className="flex items-center gap-4 pt-2">
          {song.albumArtUrl ? (
            <img src={song.albumArtUrl} alt="" className="w-24 h-24 rounded-md object-cover shrink-0" />
          ) : (
            <div className="w-24 h-24 rounded-md bg-slate-700 shrink-0" />
          )}
          <div className="min-w-0">
            <div className="text-xl text-slate-200 truncate">{song.name}</div>
            {song.artists && <div className="truncate">{song.artists}</div>}
            {paused && <span className="text-sm text-amber-500">Paused</span>}
            {!playerConnected && <p className="text-sm text-slate-500">The host's player is not connected.</p>}
          </div>
        </div>
      )}
    </section>
  );
};
