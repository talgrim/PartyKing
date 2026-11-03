import { ReactNode } from 'react';
import { PlayPauseButton } from './PlayPauseButton';
import SkipNextIcon from '@mui/icons-material/SkipNext';
import { iconButtonClass } from './controlStyles';
import { VolumeControl } from './VolumeControl';
import { useTranslation } from 'react-i18next';

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

/** Play/Pause and Next song (and the volume, when given) in a card, for whoever may control the player. */
export const PlayerControls = ({
  paused,
  disabled,
  hasSong,
  onToggle,
  onNext,
  volume,
  onVolumeChange,
}: Props): ReactNode => {
  const { t } = useTranslation();

  return (
    <div className="flex items-center justify-center gap-4 mx-2 mt-4 p-3 bg-slate-900 rounded-lg">
      {volume !== undefined && onVolumeChange && <VolumeControl volume={volume} onChange={onVolumeChange} />}
      <PlayPauseButton paused={paused} disabled={disabled || !hasSong} onToggle={onToggle} />
      <button
        onClick={onNext}
        aria-label={t('player.nextSong')}
        title={t('player.nextSong')}
        disabled={disabled}
        className={iconButtonClass}
      >
        <SkipNextIcon fontSize="small" />
      </button>
    </div>
  );
};
