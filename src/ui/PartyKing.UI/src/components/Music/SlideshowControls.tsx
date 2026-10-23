import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import FullscreenIcon from '@mui/icons-material/Fullscreen';
import { ReactNode } from 'react';

type Props = {
  open: boolean;
  onToggle: () => void;
  onMaximize: () => void;
  className?: string;
};

const buttonClass =
  'flex h-9 w-9 items-center justify-center border border-slate-400 bg-slate-900 text-slate-200 hover:bg-slate-700 cursor-pointer';

/** Icon buttons that sit on the edge of the slideshow drawer: hide or show it, and maximize it. */
export const SlideshowControls = ({ open, onToggle, onMaximize, className = '' }: Props): ReactNode => (
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
      onClick={onMaximize}
      aria-label="Maximize"
      title="Maximize"
      className={`${buttonClass} rounded-md`}
    >
      <FullscreenIcon />
    </button>
  </div>
);
