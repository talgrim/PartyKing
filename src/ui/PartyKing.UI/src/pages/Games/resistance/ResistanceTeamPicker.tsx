import { ReactNode, useState } from 'react';
import { ResistancePlayerDto } from '@/service/endpoints/games/types';
import { useTranslation } from 'react-i18next';

type Props = {
  players: ResistancePlayerDto[];
  /** How many players the mission needs. */
  size: number;
  onSubmit: (playerIds: string[]) => Promise<void>;
};

/**
 * The leader's team selection. The picks stay on this device until "Submit the team for a vote", which sends them
 * and locks the team, so the leader can change their mind until then.
 */
export const ResistanceTeamPicker = ({ players, size, onSubmit }: Props): ReactNode => {
  const { t } = useTranslation();
  const [picked, setPicked] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const full = picked.length >= size;

  const toggle = (id: string) =>
    setPicked((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]));

  const submit = async () => {
    setSubmitting(true);
    try {
      await onSubmit(picked);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <fieldset className="w-full flex flex-col gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <legend className="sr-only">{t('resistance.teamPicker.title')}</legend>

      <div className="flex items-baseline justify-between gap-2">
        <h2 className="text-lg font-semibold text-slate-100" aria-hidden="true">
          {t('resistance.teamPicker.title')}
        </h2>
        <span
          className={`text-sm font-semibold tabular-nums ${full ? 'text-emerald-400' : 'text-slate-400'}`}
          aria-live="polite"
        >
          {picked.length}/{size}
        </span>
      </div>

      <div
        role="progressbar"
        aria-label={t('resistance.teamPicker.picked')}
        aria-valuemin={0}
        aria-valuemax={size}
        aria-valuenow={picked.length}
        className="flex gap-1"
      >
        {Array.from({ length: size }, (_, i) => (
          <span
            key={i}
            className={`h-1.5 flex-1 rounded-full transition-colors ${
              i < picked.length ? 'bg-emerald-400' : 'bg-slate-700'
            }`}
          />
        ))}
      </div>

      <ul className="flex flex-col gap-2">
        {players.map((player) => {
          const checked = picked.includes(player.id);
          const disabled = submitting || (!checked && full);

          return (
            <li key={player.id}>
              <label
                className={`group flex items-center gap-3 rounded-lg px-3 py-2.5 select-none transition-colors ring-1 ${
                  checked
                    ? 'bg-emerald-500/15 ring-emerald-400 text-slate-100'
                    : 'bg-slate-800 ring-slate-700 text-slate-200 hover:bg-slate-700'
                } ${disabled ? 'opacity-40 cursor-not-allowed hover:bg-slate-800' : 'cursor-pointer'} focus-within:ring-2 focus-within:ring-emerald-300`}
              >
                <input
                  type="checkbox"
                  className="sr-only"
                  checked={checked}
                  disabled={disabled}
                  onChange={() => toggle(player.id)}
                />
                <span
                  aria-hidden="true"
                  className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-sm font-bold transition-colors ${
                    checked ? 'bg-emerald-400 text-slate-900' : 'bg-slate-900 text-transparent ring-1 ring-slate-600'
                  }`}
                >
                  ✓
                </span>
                <span className="truncate font-medium">{player.name}</span>
              </label>
            </li>
          );
        })}
      </ul>

      <button
        type="button"
        disabled={picked.length !== size || submitting}
        onClick={() => void submit()}
        className="mt-1 rounded-lg bg-emerald-500 px-4 py-3 font-semibold text-slate-900 shadow transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
      >
        {t('resistance.teamPicker.submit')}
      </button>
    </fieldset>
  );
};
