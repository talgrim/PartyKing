import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { WheelNumberInput } from '@/components/common/WheelNumberInput';
import { settingsInputClass, settingsLabelClass } from '@/components/Music/settingsStyles';
import { Sex } from './bacCalculation';
import { AGE_RANGE, BacState, HEIGHT_RANGE, WEIGHT_RANGE } from './bacStorage';

type Props = {
  state: BacState;
  update: (patch: Partial<BacState>) => void;
};

const SEXES: readonly Sex[] = ['male', 'female'];

/** Who is drinking (the content of the "About you" section): the weight and the sex are required, the height and the age make the estimate more accurate. */
export const BacProfileForm = ({ state, update }: Props): ReactNode => {
  const { t } = useTranslation();

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <label className="text-sm text-slate-300">
          <span className={settingsLabelClass}>{t('bac.weight')}</span>
          <WheelNumberInput
            className={settingsInputClass}
            inputMode="decimal"
            min={WEIGHT_RANGE.min}
            max={WEIGHT_RANGE.max}
            value={state.weightKg}
            onChange={(weightKg) => update({ weightKg })}
          />
        </label>
        <fieldset>
          <legend className={settingsLabelClass}>{t('bac.sex')}</legend>
          <div className="flex gap-3 py-2 text-sm text-slate-300">
            {SEXES.map((sex) => (
              <label key={sex} className="flex items-center gap-1.5">
                <input
                  type="radio"
                  name="bac-sex"
                  className="accent-amber-600"
                  checked={state.sex === sex}
                  onChange={() => update({ sex })}
                />
                {t(`bac.${sex}`)}
              </label>
            ))}
          </div>
        </fieldset>
      </div>
      <p className="text-xs text-slate-400">{t('bac.moreAccurate')}</p>
      <div className="grid grid-cols-2 gap-3">
        <label className="text-sm text-slate-300">
          <span className={settingsLabelClass}>{t('bac.height')}</span>
          <WheelNumberInput
            className={settingsInputClass}
            inputMode="decimal"
            min={HEIGHT_RANGE.min}
            max={HEIGHT_RANGE.max}
            value={state.heightCm}
            onChange={(heightCm) => update({ heightCm })}
          />
        </label>
        <label className="text-sm text-slate-300">
          <span className={settingsLabelClass}>{t('bac.age')}</span>
          <WheelNumberInput
            className={settingsInputClass}
            inputMode="numeric"
            min={AGE_RANGE.min}
            max={AGE_RANGE.max}
            value={state.age}
            onChange={(age) => update({ age })}
          />
        </label>
      </div>
    </div>
  );
};
