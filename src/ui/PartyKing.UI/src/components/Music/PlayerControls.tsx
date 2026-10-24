import { ReactNode } from 'react';
import { PlayPauseButton } from './PlayPauseButton';
import SkipNextIcon from '@mui/icons-material/SkipNext';
import { iconButtonClass } from './controlStyles';
import { VolumeControl } from './VolumeControl';

type Props = {
  paused: boolean;
  /** Nothing can be controlled, e.g. the player is not running. */
  disabled: boolean;
  hasSong: boolean;
  onToggle: () => void;
  onNext: () => void;
  /** The volume control is shown only when both are given: the volume lives in the player's own browser tab. */
  volume?: number;
  onVolumeChange?: (volume: number) => void;
};

/** Play/Pause and Next song, for whoever is allowed to control the player. */
export const PlayerControls = ({
  paused,
  disabled,
  hasSong,
  onToggle,
  onNext,
  volume,
  onVolumeChange,
}: Props): ReactNode => (
  <div className="flex items-center justify-center gap-4 px-2 pt-2">
    {volume !== undefined && onVolumeChange && <VolumeControl volume={volume} onChange={onVolumeChange} />}
    <PlayPauseButton paused={paused} disabled={disabled || !hasSong} onToggle={onToggle} />
    <button
      onClick={onNext}
      aria-label="Next song"
      title="Next song"
      disabled={disabled}
      className={iconButtonClass}
    >
      <SkipNextIcon fontSize="small" />
    </button>
  </div>
);
