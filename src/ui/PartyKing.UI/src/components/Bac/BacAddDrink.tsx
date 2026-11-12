import { ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import AddIcon from '@mui/icons-material/Add';
import { DRINK_KINDS, StoredDrink } from './bacStorage';
import { DrinkDraft, draftFromPreset, drinkFromDraft, isDraftValid } from './bacDrinkDraft';
import { BacDrinkFields } from './BacDrinkFields';

type Props = {
  exactTime: boolean;
  now: number;
  onAdd: (drink: StoredDrink) => void;
};

/**
 * The form that puts a drink on the list. A kind fills in its usual serving, which can be changed in the row of fields
 * before the + icon at its end adds the drink; the list then keeps the entry as it is.
 */
export const BacAddDrink = ({ exactTime, now, onAdd }: Props): ReactNode => {
  const { t } = useTranslation();
  const [draft, setDraft] = useState<DrinkDraft>(() => draftFromPreset('beer'));

  const add = () => {
    const drink = drinkFromDraft(draft, exactTime, Date.now());
    if (!drink) return;
    onAdd(drink);
    // What was drunk stays for the next one; only the time starts over
    setDraft((current) => ({ ...current, minutesAgo: 0, exactAt: null }));
  };

  return (
    <div className="space-y-2" role="group" aria-label={t('bac.addDrink')}>
      <div className="flex flex-wrap gap-2">
        {DRINK_KINDS.map((kind) => (
          <button
            key={kind}
            type="button"
            aria-pressed={draft.kind === kind}
            className={`px-3 py-1.5 text-sm rounded-md cursor-pointer ${draft.kind === kind ? 'bg-amber-600 text-slate-100' : 'bg-slate-700 hover:bg-slate-600'}`}
            onClick={() => setDraft((current) => ({ ...draftFromPreset(kind), minutesAgo: current.minutesAgo, exactAt: current.exactAt }))}
          >
            {t(`bac.kinds.${kind}`)}
          </button>
        ))}
      </div>
      <BacDrinkFields
        draft={draft}
        onChange={(patch) => setDraft((current) => ({ ...current, ...patch }))}
        exactTime={exactTime}
        now={now}
        action={
          <button
            type="button"
            aria-label={t('bac.addDrinkButton')}
            title={t('bac.addDrinkButton')}
            disabled={!isDraftValid(draft)}
            onClick={add}
            className="flex size-8 items-center justify-center bg-amber-600 text-slate-100 rounded-md cursor-pointer hover:bg-amber-500 disabled:opacity-50 disabled:cursor-default disabled:hover:bg-amber-600"
          >
            <AddIcon fontSize="small" />
          </button>
        }
      />
    </div>
  );
};
