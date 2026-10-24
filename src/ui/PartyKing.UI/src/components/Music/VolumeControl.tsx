import VolumeDownIcon from '@mui/icons-material/VolumeDown';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import { pillClass, roundedClass } from './controlStyles';

export const VOLUME_STEP = 0.1;

type Props = {
  /** 0 to 1. */
  volume: number;
  onChange: (volume: number) => void;
};

// Rounded to a whole percent so repeated steps don't drift (0.1 + 0.2 = 0.30000000000000004).
const step = (volume: number, delta: number) => Math.min(1, Math.max(0, Math.round((volume + delta) * 100) / 100));

const buttonClass =
  `h-8 w-8 ${roundedClass} text-slate-200 cursor-pointer inline-flex items-center justify-center hover:bg-slate-600 disabled:opacity-40 disabled:cursor-default disabled:hover:bg-transparent`;

/** Lowers or raises the volume of the player in steps of 10%, and shows it in between. */
export const VolumeControl = ({ volume, onChange }: Props) => (
  <div className={`${pillClass} gap-1 px-1`} role="group" aria-label="Volume">
    <button
      type="button"
      aria-label="Volume down"
      className={buttonClass}
      disabled={volume <= 0}
      onClick={() => onChange(step(volume, -VOLUME_STEP))}
    >
      <VolumeDownIcon fontSize="small" />
    </button>
    <span className="w-12 select-none text-center text-sm font-medium tabular-nums text-slate-200" aria-live="polite">
      {Math.round(volume * 100)}%
    </span>
    <button
      type="button"
      aria-label="Volume up"
      className={buttonClass}
      disabled={volume >= 1}
      onClick={() => onChange(step(volume, VOLUME_STEP))}
    >
      <VolumeUpIcon fontSize="small" />
    </button>
  </div>
);
