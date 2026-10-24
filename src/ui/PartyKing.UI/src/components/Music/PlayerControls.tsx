import { ReactNode } from 'react';
import { PlayPauseButton } from './PlayPauseButton';
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
  <div className="flex items-center gap-4 px-2 pt-2">
    {volume !== undefined && onVolumeChange && <VolumeControl volume={volume} onChange={onVolumeChange} />}
    <PlayPauseButton paused={paused} disabled={disabled || !hasSong} onToggle={onToggle} />
    <button
      onClick={onNext}
      disabled={disabled}
      className="h-10 px-3 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50 inline-flex items-center justify-center"
    >
      Next song
    </button>
  </div>
);
