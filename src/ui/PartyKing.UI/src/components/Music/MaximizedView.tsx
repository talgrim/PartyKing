import CloseIcon from '@mui/icons-material/Close';
import { ReactNode, useEffect } from 'react';
import { NowPlayingOverlay } from '@/components/Music/NowPlayingOverlay';
import { LyricsView } from '@/components/Music/LyricsView';
import { QueueList } from '@/components/Music/QueueList';
import { SlideshowView } from '@/pages/Slideshow/SlideshowView';
import { LyricsState } from '@/service/endpoints/lyrics/useLyrics';
import { NowPlayingDto } from '@/service/endpoints/player/types';

type Props = {
  sessionId: string;
  nowPlaying: NowPlayingDto | undefined;
  /** Show the lyrics instead of the slideshow. */
  showLyrics: boolean;
  lyrics: LyricsState;
  onResyncLyrics?: () => void;
  onExit: () => void;
};

/**
 * The slideshow (or the lyrics, as chosen in the drawer) across the whole screen with only the artist and title of the song and the queue on the right, for
 * a screen at the party. It covers the session page without replacing it, so the host's player underneath keeps
 * playing.
 */
export const MaximizedView = ({ sessionId, nowPlaying, showLyrics, lyrics, onResyncLyrics, onExit }: Props): ReactNode => {
  useEffect(() => {
    const exitOnEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onExit();
    };
    document.addEventListener('keydown', exitOnEscape);
    return () => document.removeEventListener('keydown', exitOnEscape);
  }, [onExit]);

  return (
    <div role="dialog" aria-label={showLyrics ? 'Lyrics' : 'Slideshow'} className="fixed inset-0 z-50 flex flex-col bg-black text-slate-400 md:flex-row">
      <div className="relative flex h-[45dvh] min-w-0 shrink-0 flex-col md:h-auto md:flex-1 md:shrink">
        <div className="min-h-0 flex-1">
          {showLyrics ? <LyricsView lyrics={lyrics} nowPlaying={nowPlaying} onResync={onResyncLyrics} /> : <SlideshowView sessionId={sessionId} />}
        </div>
        <NowPlayingOverlay nowPlaying={nowPlaying} />
        <button
          type="button"
          onClick={onExit}
          aria-label="Exit full view"
          className="absolute top-2 left-2 rounded-full bg-black/50 p-2 text-white opacity-40 hover:opacity-100 cursor-pointer"
        >
          <CloseIcon />
        </button>
      </div>
      <aside aria-label="Queue" className="min-h-0 w-full flex-1 overflow-y-auto bg-slate-800 pt-4 md:w-80 md:flex-none md:shrink-0 lg:w-96">
        <QueueList sessionId={sessionId} large />
      </aside>
    </div>
  );
};
