import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { BacState } from './bacStorage';
import { drinkAgain, newestFirst } from './bacDrinkDraft';
import { BacAddDrink } from './BacAddDrink';
import { BacDrinkEntry } from './BacDrinkEntry';

/** Room for about ten entries (each is 3.5rem high with a gap of 0.5rem); a longer list scrolls inside its panel. */
const LIST_MAX_HEIGHT_CLASS = 'max-h-[39.5rem]';

type Props = {
  state: BacState;
  update: (patch: Partial<BacState>) => void;
  now: number;
};

/**
 * The drinks of the evening, the newest on top: add one, then change it, delete it or note the same drink again with its
 * icons. A long list scrolls in a panel of about ten entries.
 */
export const BacDrinkList = ({ state, update, now }: Props): ReactNode => {
  const { t } = useTranslation();
  const { drinks, exactTime } = state;

  return (
    <div className="space-y-3">
      <label className="flex items-center gap-2 text-sm text-slate-300">
        <input
          type="checkbox"
          className="size-4 accent-amber-600"
          checked={exactTime}
          onChange={(e) => update({ exactTime: e.target.checked })}
        />
        {t('bac.exactTime')}
      </label>
      <BacAddDrink exactTime={exactTime} now={now} onAdd={(drink) => update({ drinks: [...drinks, drink] })} />
      {drinks.length === 0 ? (
        <p className="text-sm text-slate-400">{t('bac.noDrinks')}</p>
      ) : (
        <ul className={`${LIST_MAX_HEIGHT_CLASS} space-y-2 overflow-y-auto pr-1`} aria-label={t('bac.drinks')}>
          {newestFirst(drinks).map((drink) => (
            <BacDrinkEntry
              key={drink.id}
              drink={drink}
              exactTime={exactTime}
              now={now}
              onChange={(edited) => update({ drinks: drinks.map((other) => (other.id === drink.id ? edited : other)) })}
              onRemove={() => update({ drinks: drinks.filter((other) => other.id !== drink.id) })}
              onAgain={() => update({ drinks: [...drinks, drinkAgain(drink, Date.now())] })}
            />
          ))}
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
    </div>
  );
};
