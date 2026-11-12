import { ABV_RANGE, DRINK_PRESETS, DrinkKind, StoredDrink, VOLUME_RANGE, inRange, newDrinkId } from './bacStorage';
import { fromMinutesAgo } from './bacTime';

/** A drink as it is typed in, before it becomes (or after it is taken back from) an entry of the list. */
export type DrinkDraft = {
  kind: DrinkKind;
  volumeMl: number | null;
  abvPercent: number | null;
  /** How long ago, counted from the moment the draft is saved; null while it was not typed in. */
  minutesAgo: number | null;
  /** A moment picked on the clock, or the start the drink being edited already has; null means now. */
  exactAt: number | null;
};

/** A new drink of a kind: its usual serving, started now. */
export const draftFromPreset = (kind: DrinkKind): DrinkDraft => ({ kind, ...DRINK_PRESETS[kind], minutesAgo: 0, exactAt: null });

/** An entry taken back for editing: its start stays as it was until the time is changed. */
export const draftFromDrink = ({ kind, volumeMl, abvPercent, startedAt }: StoredDrink): DrinkDraft => ({
  kind,
  volumeMl,
  abvPercent,
  minutesAgo: null,
  exactAt: startedAt,
});

export const isDraftValid = (draft: DrinkDraft): boolean =>
  inRange(draft.volumeMl, VOLUME_RANGE) && inRange(draft.abvPercent, ABV_RANGE);

/** The entry a draft stands for, or null while its volume or strength is missing or out of range. */
export const drinkFromDraft = (draft: DrinkDraft, exactTime: boolean, now: number, id = newDrinkId()): StoredDrink | null => {
  const { kind, volumeMl, abvPercent } = draft;
  if (!inRange(volumeMl, VOLUME_RANGE) || !inRange(abvPercent, ABV_RANGE)) return null;

  const startedAt = exactTime || draft.minutesAgo === null ? (draft.exactAt ?? now) : fromMinutesAgo(draft.minutesAgo, now);
  return { id, kind, volumeMl, abvPercent, startedAt };
};

/** The same drink again, started now: the quick way to note another round. */
export const drinkAgain = (drink: StoredDrink, now: number): StoredDrink => ({ ...drink, id: newDrinkId(), startedAt: now });

/** The drinks with the one started last first. Drinks started at the same moment keep the later one added on top. */
export const newestFirst = (drinks: readonly StoredDrink[]): StoredDrink[] =>
  drinks
    .map((drink, index) => ({ drink, index }))
    .sort((a, b) => b.drink.startedAt - a.drink.startedAt || b.index - a.index)
    .map(({ drink }) => drink);
