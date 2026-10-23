import PauseIcon from '@mui/icons-material/Pause';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';

type Props = {
  paused: boolean;
  disabled?: boolean;
  onToggle: () => void;
};

export const PlayPauseButton = ({ paused, disabled, onToggle }: Props) => (
  <button
    onClick={onToggle}
    disabled={disabled}
    className="p-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50 inline-flex items-center gap-1"
  >
    {paused ? <PlayArrowIcon fontSize="small" /> : <PauseIcon fontSize="small" />}
    {paused ? 'Play' : 'Pause'}
  </button>
);
