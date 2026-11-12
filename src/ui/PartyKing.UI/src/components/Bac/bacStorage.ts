import { BacDrink, Sex } from './bacCalculation';

export const BAC_STORAGE_KEY = 'partyking.bacCalculator';

const STORAGE_VERSION = 1;

/** A drink older than this has left the blood long ago, so it is not worth keeping. */
const KEEP_DRINKS_MS = 24 * 60 * 60 * 1000;

export const WEIGHT_RANGE = { min: 30, max: 300 };
export const HEIGHT_RANGE = { min: 100, max: 250 };
export const AGE_RANGE = { min: 10, max: 120 };
export const VOLUME_RANGE = { min: 1, max: 5000 };
export const ABV_RANGE = { min: 0, max: 100 };

export type DrinkKind = 'beer' | 'wine' | 'shot' | 'custom';

export const DRINK_KINDS: readonly DrinkKind[] = ['beer', 'wine', 'shot', 'custom'];

/** The usual serving of each kind, which is what a new drink starts as. */
export const DRINK_PRESETS: Record<DrinkKind, Pick<BacDrink, 'volumeMl' | 'abvPercent'>> = {
  beer: { volumeMl: 500, abvPercent: 5 },
  wine: { volumeMl: 150, abvPercent: 12 },
  shot: { volumeMl: 40, abvPercent: 40 },
  custom: { volumeMl: 330, abvPercent: 5 },
};

export type StoredDrink = BacDrink & { id: string; kind: DrinkKind };

export type BacState = {
  weightKg: number | null;
  sex: Sex;
  heightCm: number | null;
  age: number | null;
  drinks: StoredDrink[];
  /** Whether a drink's time is picked on a clock instead of entered as minutes ago. */
  exactTime: boolean;
};

export const DEFAULT_BAC_STATE: BacState = {
  weightKg: null,
  sex: 'male',
  heightCm: null,
  age: null,
  drinks: [],
  exactTime: false,
};

export const newDrinkId = (): string => `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;

export const inRange = (value: unknown, { min, max }: { min: number; max: number }): value is number =>
  typeof value === 'number' && Number.isFinite(value) && value >= min && value <= max;

const rangedOrNull = (value: unknown, range: { min: number; max: number }): number | null => (inRange(value, range) ? value : null);

const isDrinkKind = (value: unknown): value is DrinkKind => DRINK_KINDS.some((kind) => kind === value);

const toDrink = (value: unknown, now: number): StoredDrink | null => {
  if (typeof value !== 'object' || value === null) return null;
  const { id, kind, volumeMl, abvPercent, startedAt } = value as Record<string, unknown>;
  if (typeof id !== 'string' || !isDrinkKind(kind)) return null;
  if (!inRange(volumeMl, VOLUME_RANGE) || !inRange(abvPercent, ABV_RANGE)) return null;
  if (typeof startedAt !== 'number' || !Number.isFinite(startedAt) || startedAt < now - KEEP_DRINKS_MS) return null;
  return { id, kind, volumeMl, abvPercent, startedAt };
};

/**
 * The saved state, or the default when nothing valid is saved. It never throws on bad input: a field out of its range
 * is dropped alone and drinks older than a day are forgotten.
 */
export const parseBacState = (raw: string | null, now: number): BacState => {
  if (raw === null) return DEFAULT_BAC_STATE;
  try {
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null) return DEFAULT_BAC_STATE;
    const stored = parsed as Record<string, unknown>;
    if (stored.version !== STORAGE_VERSION) return DEFAULT_BAC_STATE;

    return {
      weightKg: rangedOrNull(stored.weightKg, WEIGHT_RANGE),
      sex: stored.sex === 'female' ? 'female' : 'male',
      heightCm: rangedOrNull(stored.heightCm, HEIGHT_RANGE),
      age: rangedOrNull(stored.age, AGE_RANGE),
      drinks: Array.isArray(stored.drinks)
        ? stored.drinks.map((drink) => toDrink(drink, now)).filter((drink): drink is StoredDrink => drink !== null)
        : [],
      exactTime: stored.exactTime === true,
    };
  } catch {
    return DEFAULT_BAC_STATE;
  }
};

export const serializeBacState = (state: BacState): string => JSON.stringify({ version: STORAGE_VERSION, ...state });

export const readBacState = (now: number): BacState => {
  try {
    return parseBacState(localStorage.getItem(BAC_STORAGE_KEY), now);
  } catch {
    return DEFAULT_BAC_STATE;
  }
};

export const writeBacState = (state: BacState): void => {
  try {
    localStorage.setItem(BAC_STORAGE_KEY, serializeBacState(state));
  } catch {
    // Storage is unavailable: the calculator still works, it just forgets on a reload
  }
};
