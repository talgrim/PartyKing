import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import FullscreenIcon from '@mui/icons-material/Fullscreen';
import LyricsIcon from '@mui/icons-material/Lyrics';
import SlideshowIcon from '@mui/icons-material/Slideshow';
import SubtitlesOffIcon from '@mui/icons-material/SubtitlesOff';
import { ReactNode } from 'react';

type Props = {
  open: boolean;
  onToggle: () => void;
  onMaximize: () => void;
  /** The drawer shows the lyrics instead of the slideshow. */
  lyricsActive: boolean;
  /** The song playing has lyrics; the button stays usable without, but says so. */
  lyricsAvailable: boolean;
  onToggleLyrics: () => void;
  className?: string;
};

const buttonClass =
  'flex h-9 w-9 items-center justify-center border border-slate-400 bg-slate-900 text-slate-200 hover:bg-slate-700 cursor-pointer';

/**
 * Icon buttons that sit on the edge of the slideshow drawer: hide or show it, switch it between the slideshow and the
 * lyrics, and maximize it.
 */
export const SlideshowControls = ({
  open,
  onToggle,
  onMaximize,
  lyricsActive,
  lyricsAvailable,
  onToggleLyrics,
  className = '',
}: Props): ReactNode => (
  <div className={`flex flex-col gap-1 ${className}`}>
    <button
      type="button"
      onClick={onToggle}
      aria-label={open ? 'Hide slideshow' : 'Show slideshow'}
      title={open ? 'Hide slideshow' : 'Show slideshow'}
      className={`${buttonClass} rounded-md`}
    >
      {open ? <ChevronLeftIcon /> : <ChevronRightIcon />}
    </button>
    <button
      type="button"
      onClick={onToggleLyrics}
      aria-pressed={lyricsActive}
      aria-label={lyricsActive ? 'Show slideshow' : 'Show lyrics'}
      title={
        lyricsActive
          ? 'Show slideshow'
          : lyricsAvailable
            ? 'Show lyrics'
            : 'Show lyrics (none found for this song)'
      }
      className={`${buttonClass} rounded-md ${lyricsActive ? 'bg-slate-600' : ''} ${lyricsAvailable || lyricsActive ? '' : 'text-slate-500'}`}
    >
      {lyricsActive ? <SlideshowIcon /> : lyricsAvailable ? <LyricsIcon /> : <SubtitlesOffIcon />}
    </button>
    <button
      type="button"
      onClick={onMaximize}
      aria-label="Maximize"
      title="Maximize"
      className={`${buttonClass} rounded-md`}
    >
      <FullscreenIcon />
    </button>
  </div>
);
