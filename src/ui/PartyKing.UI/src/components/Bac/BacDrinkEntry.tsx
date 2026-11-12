import { ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutline';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import EditIcon from '@mui/icons-material/Edit';
import { gramsOfAlcohol } from './bacCalculation';
import { StoredDrink } from './bacStorage';
import { DrinkDraft, draftFromDrink, drinkFromDraft, isDraftValid } from './bacDrinkDraft';
import { BacDrinkFields } from './BacDrinkFields';
import { minutesAgo } from './bacTime';

type Props = {
  drink: StoredDrink;
  exactTime: boolean;
  now: number;
  /** The entry after it was edited. */
  onChange: (drink: StoredDrink) => void;
  onRemove: () => void;
  /** The same drink once more, started now. */
  onAgain: () => void;
};

const MINUTES_PER_HOUR = 60;

const iconButtonClass = 'p-0.5 text-slate-400 cursor-pointer hover:text-slate-100';

/**
 * An entry of the drink list. It is read-only: the icons edit it (the fields open in place and are saved or dropped),
 * delete it or add the same drink again, started now.
 */
export const BacDrinkEntry = ({ drink, exactTime, now, onChange, onRemove, onAgain }: Props): ReactNode => {
  const { t, i18n } = useTranslation();
  const [draft, setDraft] = useState<DrinkDraft | null>(null);
  const name = t(`bac.kinds.${drink.kind}`);

  if (draft) {
    const edited = drinkFromDraft(draft, exactTime, Date.now(), drink.id);
    return (
      <li className="p-2 space-y-2 bg-slate-900/60 rounded-md">
        <span className="font-medium text-slate-200">{name}</span>
        <BacDrinkFields
          draft={draft}
          onChange={(patch) => setDraft({ ...draft, ...patch })}
          exactTime={exactTime}
          now={now}
          labelPrefix={`${name}: `}
        />
        <div className="flex gap-2">
          <button
            type="button"
            aria-label={t('bac.saveDrink', { drink: name })}
            title={t('common.save')}
            disabled={!isDraftValid(draft)}
            className="p-1 text-amber-500 cursor-pointer hover:text-amber-400 disabled:opacity-40 disabled:cursor-default"
            onClick={() => {
              if (edited) onChange(edited);
              setDraft(null);
            }}
          >
            <CheckIcon fontSize="small" />
          </button>
          <button
            type="button"
            aria-label={t('bac.cancelEdit', { drink: name })}
            title={t('common.cancel')}
            className={iconButtonClass}
            onClick={() => setDraft(null)}
          >
            <CloseIcon fontSize="small" />
          </button>
        </div>
      </li>
    );
  }

  const minutes = minutesAgo(drink.startedAt, now);
  const ago =
    minutes < 1
      ? t('bac.justNow')
      : minutes < MINUTES_PER_HOUR
        ? t('bac.agoMinutes', { minutes })
        : t('bac.agoHours', { hours: Math.floor(minutes / MINUTES_PER_HOUR), minutes: minutes % MINUTES_PER_HOUR });
  const clock = new Date(drink.startedAt).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' });

  return (
    <li className="flex items-center justify-between gap-2 p-2 bg-slate-900/60 rounded-md">
      <div className="min-w-0">
        <div className="font-medium text-slate-200">{name}</div>
        <div className="text-xs text-slate-400">
          {t('bac.drinkSummary', {
            volume: drink.volumeMl.toLocaleString(i18n.language),
            abv: drink.abvPercent.toLocaleString(i18n.language),
            grams: gramsOfAlcohol(drink).toLocaleString(i18n.language, { maximumFractionDigits: 1 }),
          })}
          {' · '}
          {ago} ({clock})
        </div>
      </div>
      <div className="flex shrink-0 gap-1">
        <button
          type="button"
          aria-label={t('bac.addAgain', { drink: name })}
          title={t('bac.addAgain', { drink: name })}
          className={iconButtonClass}
          onClick={onAgain}
        >
          <AddCircleOutlineIcon fontSize="small" />
        </button>
        <button
          type="button"
          aria-label={t('bac.editDrink', { drink: name })}
          title={t('bac.editDrink', { drink: name })}
          className={iconButtonClass}
          onClick={() => setDraft(draftFromDrink(drink))}
        >
          <EditIcon fontSize="small" />
        </button>
        <button
          type="button"
          aria-label={t('bac.removeDrink', { drink: name })}
          title={t('bac.removeDrink', { drink: name })}
          className={`${iconButtonClass} hover:text-red-400!`}
          onClick={onRemove}
        >
          <DeleteOutlineIcon fontSize="small" />
        </button>
      </div>
    </li>
  );
};
