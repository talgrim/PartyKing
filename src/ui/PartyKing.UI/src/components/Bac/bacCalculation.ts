export type Sex = 'male' | 'female';

export type BacProfile = {
  weightKg: number;
  sex: Sex;
  /** Together with the age it switches the estimate from the Widmark factor to Watson's total body water. */
  heightCm: number | null;
  age: number | null;
};

export type BacDrink = {
  volumeMl: number;
  /** Alcohol by volume, in percent (5 for a 5% beer). */
  abvPercent: number;
  /** When the drink was started, in epoch milliseconds. */
  startedAt: number;
};

export type BacEstimate = {
  /** In promille (g of alcohol per kg of blood), the unit of the Widmark formula; 1‰ is 0.1%. */
  nowPromille: number;
  peakPromille: number;
  /** When the peak is reached, in epoch milliseconds; null when nothing was drunk. */
  peakAt: number | null;
  /** When the blood is free of alcohol again, in epoch milliseconds; null when it already is. */
  soberAt: number | null;
};

/** Grams of ethanol in a millilitre. */
export const ETHANOL_DENSITY = 0.789;

/** Widmark's average distribution factors (NIST: 0.68 ± 0.17 for men, 0.55 ± 0.11 for women). */
export const WIDMARK_FACTOR: Record<Sex, number> = { male: 0.68, female: 0.55 };

/** What the body clears per hour, in promille. NIST puts the average at 0.18; the textbook value is the cautious 0.15. */
export const ELIMINATION_PER_HOUR = 0.15;

/** How long a drink takes to get into the blood; it is absorbed evenly over this time. */
export const ABSORPTION_MINUTES = 30;

/** Water makes up about 80% of blood, which turns total body water into Widmark's factor. */
const BLOOD_WATER_FRACTION = 0.8;

const MS_PER_MINUTE = 60_000;
const MINUTES_PER_HOUR = 60;

/** The simulation runs minute by minute; no estimate has to look further ahead than this. */
const MAX_SIMULATED_MINUTES = 7 * 24 * MINUTES_PER_HOUR;

/**
 * Watson, Watson and Batt (Am J Clin Nutr, 1980), litres of total body water from the height in cm, the weight in kg and
 * the age. The age term of the male equation is printed as 0.09156 on some sites, a transposition of the 0.09516 of the paper.
 */
export const totalBodyWaterLitres = ({ sex, weightKg, heightCm, age }: BacProfile & { heightCm: number; age: number }): number =>
  sex === 'male'
    ? 2.447 - 0.09516 * age + 0.1074 * heightCm + 0.3362 * weightKg
    : -2.097 + 0.1069 * heightCm + 0.2466 * weightKg;

/** Widmark's r: Watson's total body water when the height and the age are known, the average factor of the sex otherwise. */
export const distributionFactor = (profile: BacProfile): number => {
  const { heightCm, age, weightKg } = profile;
  if (heightCm === null || age === null) return WIDMARK_FACTOR[profile.sex];
  return totalBodyWaterLitres({ ...profile, heightCm, age }) / (BLOOD_WATER_FRACTION * weightKg);
};

/** Whether the estimate rests on Watson's total body water, which needs both the height and the age. */
export const usesTotalBodyWater = ({ heightCm, age }: Pick<BacProfile, 'heightCm' | 'age'>): boolean =>
  heightCm !== null && age !== null;

export const gramsOfAlcohol = ({ volumeMl, abvPercent }: Pick<BacDrink, 'volumeMl' | 'abvPercent'>): number =>
  volumeMl * (abvPercent / 100) * ETHANOL_DENSITY;

/** The pure alcohol of all the drinks together, in grams. */
export const totalGramsOfAlcohol = (drinks: readonly Pick<BacDrink, 'volumeMl' | 'abvPercent'>[]): number =>
  drinks.reduce((sum, drink) => sum + gramsOfAlcohol(drink), 0);

const absorbedFraction = (drink: BacDrink, at: number): number =>
  Math.min(1, Math.max(0, (at - drink.startedAt) / (ABSORPTION_MINUTES * MS_PER_MINUTE)));

/**
 * The blood alcohol content now, its peak and when it reaches zero, from the Widmark formula run minute by minute.
 * Each drink gets into the blood evenly over {@link ABSORPTION_MINUTES} and the body clears {@link ELIMINATION_PER_HOUR}
 * an hour whenever there is alcohol to clear, so a gap between drinks is handled correctly: the blood can reach zero
 * and the next drink starts from there, instead of from a negative value.
 */
export const estimateBac = (profile: BacProfile, drinks: readonly BacDrink[], now: number): BacEstimate => {
  if (drinks.length === 0) return { nowPromille: 0, peakPromille: 0, peakAt: null, soberAt: null };

  const factor = distributionFactor(profile);
  const perMinuteClearance = ELIMINATION_PER_HOUR / MINUTES_PER_HOUR;
  // Promille one drink adds when it is absorbed completely
  const addedBy = (drink: BacDrink) => gramsOfAlcohol(drink) / (factor * profile.weightKg);

  const start = Math.min(now, ...drinks.map((drink) => drink.startedAt));
  const lastAbsorbed = Math.max(...drinks.map((drink) => drink.startedAt)) + ABSORPTION_MINUTES * MS_PER_MINUTE;

  let promille = 0;
  let nowPromille = 0;
  let peakPromille = 0;
  let peakAt: number | null = null;
  let soberAt: number | null = null;

  for (let minute = 0; minute <= MAX_SIMULATED_MINUTES; minute++) {
    const at = start + minute * MS_PER_MINUTE;
    const previous = at - MS_PER_MINUTE;
    const absorbed = drinks.reduce(
      (sum, drink) => sum + addedBy(drink) * (absorbedFraction(drink, at) - absorbedFraction(drink, previous)),
      0,
    );
    promille = minute === 0 ? 0 : Math.max(0, promille + absorbed - perMinuteClearance);

    if (at <= now) nowPromille = promille;
    if (promille > peakPromille) {
      peakPromille = promille;
      peakAt = at;
    }
    if (at >= lastAbsorbed && promille === 0) {
      soberAt = at > now ? at : null;
      break;
    }
  }

  return { nowPromille, peakPromille, peakAt, soberAt };
};
