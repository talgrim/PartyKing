import CloseIcon from '@mui/icons-material/Close';
import QueueMusicIcon from '@mui/icons-material/QueueMusic';
import { ReactNode, useEffect } from 'react';
import { NowPlayingOverlay } from '@/components/Music/NowPlayingOverlay';
import { LyricsView } from '@/components/Music/LyricsView';
import { QueueList } from '@/components/Music/QueueList';
import { SlideshowView } from '@/pages/Slideshow/SlideshowView';
import { LyricsState } from '@/service/endpoints/lyrics/useLyrics';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import { usePersistedFlag } from '@/utils/usePersistedFlag';

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
 * a screen at the party. The queue sits below the slideshow on a portrait screen and beside it on a landscape or wide one (a phone turned sideways
 * included); either way it can be hidden, and the choice is kept in localStorage. It covers the session page without replacing it, so the host's player underneath keeps
 * playing.
 */
export const MaximizedView = ({ sessionId, nowPlaying, showLyrics, lyrics, onResyncLyrics, onExit }: Props): ReactNode => {
  const [queueOpen, setQueueOpen] = usePersistedFlag('partyking.maximizedQueueOpen', true);

  useEffect(() => {
    const exitOnEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onExit();
    };
    document.addEventListener('keydown', exitOnEscape);
    return () => document.removeEventListener('keydown', exitOnEscape);
  }, [onExit]);

  return (
    <div role="dialog" aria-label={showLyrics ? 'Lyrics' : 'Slideshow'} className="fixed inset-0 z-50 flex flex-col bg-black text-slate-400 landscape:flex-row md:flex-row">
      <div
        className={`relative flex min-w-0 flex-col landscape:h-auto landscape:flex-1 landscape:shrink md:h-auto md:flex-1 md:shrink ${
          queueOpen ? 'h-[45dvh] shrink-0' : 'min-h-0 flex-1'
        }`}
      >
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
        <button
          type="button"
          onClick={() => setQueueOpen(!queueOpen)}
          aria-label={queueOpen ? 'Hide queue' : 'Show queue'}
          aria-pressed={queueOpen}
          className="absolute top-2 left-14 rounded-full bg-black/50 p-2 text-white opacity-40 hover:opacity-100 cursor-pointer"
        >
          <QueueMusicIcon />
        </button>
      </div>
      {queueOpen && (
        <aside
          aria-label="Queue"
          className="min-h-0 w-full flex-1 overflow-y-auto bg-slate-800 pt-4 landscape:w-72 landscape:flex-none landscape:shrink-0 md:w-80 md:flex-none md:shrink-0 lg:w-96"
        >
          <QueueList sessionId={sessionId} large />
        </aside>
      )}
    </div>
  );
};
