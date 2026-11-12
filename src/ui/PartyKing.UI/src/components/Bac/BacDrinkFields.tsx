import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { WheelNumberInput } from '@/components/common/WheelNumberInput';
import { settingsInputClass } from '@/components/Music/settingsStyles';
import { ABV_RANGE, VOLUME_RANGE } from './bacStorage';
import { DrinkDraft } from './bacDrinkDraft';
import { fromLocalInputValue, minutesAgo, toLocalInputValue } from './bacTime';

type Props = {
  draft: DrinkDraft;
  onChange: (patch: Partial<DrinkDraft>) => void;
  /** Whether the time is picked on a clock instead of entered as minutes ago. */
  exactTime: boolean;
  now: number;
  /** Put before each label that screen readers read, to tell the entries apart. */
  labelPrefix?: string;
  /** Something that ends the row, like the button that adds the drink. */
  action?: ReactNode;
};

const inputClass = `${settingsInputClass} px-2! py-1!`;

/** The volume, the strength and the time of a drink: what the add form and an entry being edited have in common. */
export const BacDrinkFields = ({ draft, onChange, exactTime, now, labelPrefix = '', action }: Props): ReactNode => {
  const { t } = useTranslation();
  const minutes = draft.minutesAgo ?? (draft.exactAt === null ? 0 : minutesAgo(draft.exactAt, now));

  return (
    <div className={`grid items-end gap-2 text-xs text-slate-400 ${action ? 'grid-cols-[repeat(3,minmax(0,1fr))_auto]' : 'grid-cols-3'}`}>
      <label>
        {t('bac.volumeMl')}
        <WheelNumberInput
          className={inputClass}
          aria-label={`${labelPrefix}${t('bac.volumeMl')}`}
          min={VOLUME_RANGE.min}
          max={VOLUME_RANGE.max}
          step={10}
          value={draft.volumeMl}
          onChange={(volumeMl) => onChange({ volumeMl })}
        />
      </label>
      <label>
        {t('bac.abv')}
        <WheelNumberInput
          className={inputClass}
          aria-label={`${labelPrefix}${t('bac.abv')}`}
          min={ABV_RANGE.min}
          max={ABV_RANGE.max}
          step={0.5}
          value={draft.abvPercent}
          onChange={(abvPercent) => onChange({ abvPercent })}
        />
      </label>
      {exactTime ? (
        <label>
          {t('bac.startedAt')}
          <input
            className={inputClass}
            type="datetime-local"
            aria-label={`${labelPrefix}${t('bac.startedAt')}`}
            value={toLocalInputValue(draft.exactAt ?? now)}
            onChange={(e) => {
              const exactAt = fromLocalInputValue(e.target.value);
              if (exactAt !== null) onChange({ exactAt });
            }}
          />
        </label>
      ) : (
        <label>
          {t('bac.minutesAgo')}
          <WheelNumberInput
            className={inputClass}
            aria-label={`${labelPrefix}${t('bac.minutesAgo')}`}
            min={0}
            step={5}
            value={minutes}
            onChange={(value) => onChange({ minutesAgo: value ?? 0 })}
          />
        </label>
      )}
      {action}
    </div>
  );
};
