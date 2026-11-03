import PauseIcon from '@mui/icons-material/Pause';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { iconButtonClass } from './controlStyles';
import { useTranslation } from 'react-i18next';

type Props = {
  paused: boolean;
  disabled?: boolean;
  onToggle: () => void;
};

export const PlayPauseButton = ({ paused, disabled, onToggle }: Props) => {
  const { t } = useTranslation();
  const label = paused ? t('player.play') : t('player.pause');

  return (
    <button
      onClick={onToggle}
      aria-label={label}
      title={label}
      disabled={disabled}
      className={iconButtonClass}
    >
      {paused ? <PlayArrowIcon fontSize="small" className="text-amber-500" /> : <PauseIcon fontSize="small" className="text-amber-500" />}
    </button>
  );
};
