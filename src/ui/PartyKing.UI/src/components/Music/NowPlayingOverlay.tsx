import { ReactNode } from 'react';
import { NowPlayingDto } from '@/service/endpoints/player/types';

type Props = {
  nowPlaying: NowPlayingDto | undefined;
  /** The lyrics are shown: on a short landscape screen (a phone turned sideways) the label shrinks and moves to the top, leaving the bottom to the timing controls. */
  overLyrics?: boolean;
};

const shortScreen = '[@media(max-height:500px)]';

/** Only the artist and the title of the song playing, large enough to read from across the room. */
export const NowPlayingOverlay = ({ nowPlaying, overLyrics = false }: Props): ReactNode => {
  const song = nowPlaying?.song;
  if (!song) return null;

  const compact = overLyrics
    ? `${shortScreen}:order-first ${shortScreen}:py-1 ${shortScreen}:pl-24 ${shortScreen}:pr-28 ${shortScreen}:flex ${shortScreen}:items-baseline ${shortScreen}:gap-2`
    : '';
  const title = overLyrics ? `${shortScreen}:text-base` : '';
  const artists = overLyrics ? `${shortScreen}:text-sm` : '';

  return (
    <section aria-label="Now playing" className={`px-6 py-4 bg-black/60 text-white ${compact}`}>
      <div className={`truncate text-3xl font-semibold ${title}`}>{song.name}</div>
      {song.artists && <div className={`truncate text-xl text-slate-300 ${artists}`}>{song.artists}</div>}
    </section>
  );
};
