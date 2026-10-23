import { ReactNode } from 'react';
import { NowPlayingDto } from '@/service/endpoints/player/types';

type Props = {
  nowPlaying: NowPlayingDto | undefined;
};

/** Only the artist and the title of the song playing, large enough to read from across the room. */
export const NowPlayingOverlay = ({ nowPlaying }: Props): ReactNode => {
  const song = nowPlaying?.song;
  if (!song) return null;

  return (
    <section aria-label="Now playing" className="px-6 py-4 bg-black/60 text-white">
      <div className="truncate text-3xl font-semibold">{song.name}</div>
      {song.artists && <div className="truncate text-xl text-slate-300">{song.artists}</div>}
    </section>
  );
};
