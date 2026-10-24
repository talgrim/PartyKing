import PauseIcon from '@mui/icons-material/Pause';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { iconButtonClass } from './controlStyles';

type Props = {
  paused: boolean;
  disabled?: boolean;
  onToggle: () => void;
};

export const PlayPauseButton = ({ paused, disabled, onToggle }: Props) => (
  <button
    onClick={onToggle}
    aria-label={paused ? 'Play' : 'Pause'}
    title={paused ? 'Play' : 'Pause'}
    disabled={disabled}
    className={iconButtonClass}
  >
    {paused ? <PlayArrowIcon fontSize="small" className="text-amber-500" /> : <PauseIcon fontSize="small" className="text-amber-500" />}
  </button>
);
