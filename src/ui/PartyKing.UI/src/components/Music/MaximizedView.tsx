import CloseIcon from '@mui/icons-material/Close';
import { ReactNode, useEffect } from 'react';
import { NowPlayingOverlay } from '@/components/Music/NowPlayingOverlay';
import { QueueList } from '@/components/Music/QueueList';
import { SlideshowView } from '@/pages/Slideshow/SlideshowView';
import { NowPlayingDto } from '@/service/endpoints/player/types';

type Props = {
  sessionId: string;
  nowPlaying: NowPlayingDto | undefined;
  onExit: () => void;
};

/**
 * The slideshow across the whole screen with only the artist and title of the song and the queue on the right, for
 * a screen at the party. It covers the session page without replacing it, so the host's player underneath keeps
 * playing.
 */
export const MaximizedView = ({ sessionId, nowPlaying, onExit }: Props): ReactNode => {
  useEffect(() => {
    const exitOnEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onExit();
    };
    document.addEventListener('keydown', exitOnEscape);
    return () => document.removeEventListener('keydown', exitOnEscape);
  }, [onExit]);

  return (
    <div role="dialog" aria-label="Slideshow" className="fixed inset-0 z-50 flex bg-black text-slate-400">
      <div className="relative flex min-w-0 flex-1 flex-col">
        <div className="min-h-0 flex-1">
          <SlideshowView sessionId={sessionId} />
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
      <aside aria-label="Queue" className="w-80 shrink-0 overflow-y-auto bg-slate-800 pt-4 lg:w-96">
        <QueueList sessionId={sessionId} large />
      </aside>
    </div>
  );
};
