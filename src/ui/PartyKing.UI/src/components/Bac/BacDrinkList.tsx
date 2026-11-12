import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import CloseIcon from '@mui/icons-material/Close';
import { settingsInputClass } from '@/components/Music/settingsStyles';
import { ABV_RANGE, BacState, DRINK_KINDS, StoredDrink, VOLUME_RANGE, newDrink } from './bacStorage';
import { fromLocalInputValue, fromMinutesAgo, minutesAgo, toLocalInputValue } from './bacTime';
import { numberOrNull } from './numberInput';

type Props = {
  state: BacState;
  update: (patch: Partial<BacState>) => void;
  now: number;
};

const smallInputClass = `${settingsInputClass} px-2! py-1!`;

/** The drinks of the evening: add one of the usual kinds, then correct its volume, strength and when it was started. */
export const BacDrinkList = ({ state, update, now }: Props): ReactNode => {
  const { t } = useTranslation();
  const { drinks, exactTime } = state;

  const change = (id: string, patch: Partial<StoredDrink>) =>
    update({ drinks: drinks.map((drink) => (drink.id === id ? { ...drink, ...patch } : drink)) });

  return (
    <section className="space-y-3" aria-labelledby="bac-drinks">
      <h3 id="bac-drinks" className="font-semibold text-slate-100">
        {t('bac.drinks')}
      </h3>
      <div className="flex flex-wrap gap-2" role="group" aria-label={t('bac.addDrink')}>
        {DRINK_KINDS.map((kind) => (
          <button
            key={kind}
            type="button"
            className="px-3 py-1.5 text-sm bg-slate-700 rounded-md cursor-pointer hover:bg-slate-600"
            onClick={() => update({ drinks: [...drinks, newDrink(kind, Date.now())] })}
          >
            + {t(`bac.kinds.${kind}`)}
          </button>
        ))}
      </div>
      <label className="flex items-center gap-2 text-sm text-slate-300">
        <input
          type="checkbox"
          className="size-4 accent-amber-600"
          checked={exactTime}
          onChange={(e) => update({ exactTime: e.target.checked })}
        />
        {t('bac.exactTime')}
      </label>
      {drinks.length === 0 ? (
        <p className="text-sm text-slate-400">{t('bac.noDrinks')}</p>
      ) : (
        <ul className="space-y-2">
          {drinks.map((drink) => {
            const name = t(`bac.kinds.${drink.kind}`);
            return (
              <li key={drink.id} className="p-2 space-y-2 bg-slate-900/60 rounded-md">
                <div className="flex items-center justify-between">
                  <span className="font-medium text-slate-200">{name}</span>
                  <button
                    type="button"
                    onClick={() => update({ drinks: drinks.filter((other) => other.id !== drink.id) })}
                    aria-label={t('bac.removeDrink', { drink: name })}
                    title={t('bac.removeDrink', { drink: name })}
                    className="p-0.5 text-slate-400 hover:text-red-400 cursor-pointer"
                  >
                    <CloseIcon fontSize="small" />
                  </button>
                </div>
                <div className="grid grid-cols-3 gap-2 text-xs text-slate-400">
                  <label>
                    {t('bac.volumeMl')}
                    <input
                      className={smallInputClass}
                      type="number"
                      aria-label={`${name}: ${t('bac.volumeMl')}`}
                      min={VOLUME_RANGE.min}
                      max={VOLUME_RANGE.max}
                      value={drink.volumeMl}
                      onChange={(e) => change(drink.id, { volumeMl: numberOrNull(e.target.value) ?? 0 })}
                    />
                  </label>
                  <label>
                    {t('bac.abv')}
                    <input
                      className={smallInputClass}
                      type="number"
                      aria-label={`${name}: ${t('bac.abv')}`}
                      min={ABV_RANGE.min}
                      max={ABV_RANGE.max}
                      step="0.5"
                      value={drink.abvPercent}
                      onChange={(e) => change(drink.id, { abvPercent: numberOrNull(e.target.value) ?? 0 })}
                    />
                  </label>
                  {exactTime ? (
                    <label>
                      {t('bac.startedAt')}
                      <input
                        className={smallInputClass}
                        type="datetime-local"
                        aria-label={`${name}: ${t('bac.startedAt')}`}
                        value={toLocalInputValue(drink.startedAt)}
                        onChange={(e) => {
                          const startedAt = fromLocalInputValue(e.target.value);
                          if (startedAt !== null) change(drink.id, { startedAt });
                        }}
                      />
                    </label>
                  ) : (
                    <label>
                      {t('bac.minutesAgo')}
                      <input
                        className={smallInputClass}
                        type="number"
                        aria-label={`${name}: ${t('bac.minutesAgo')}`}
                        min={0}
                        value={minutesAgo(drink.startedAt, now)}
                        onChange={(e) => change(drink.id, { startedAt: fromMinutesAgo(numberOrNull(e.target.value) ?? 0, Date.now()) })}
                      />
                    </label>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
      {drinks.length > 0 && (
        <button
          type="button"
          className="text-sm text-slate-400 underline cursor-pointer hover:text-slate-200"
          onClick={() => update({ drinks: [] })}
        >
          {t('bac.clearDrinks')}
        </button>
      )}
    </section>
  );
};
