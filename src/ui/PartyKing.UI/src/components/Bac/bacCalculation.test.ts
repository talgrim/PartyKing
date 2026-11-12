import {
  ABSORPTION_MINUTES,
  BacDrink,
  BacProfile,
  ELIMINATION_PER_HOUR,
  distributionFactor,
  estimateBac,
  gramsOfAlcohol,
  totalBodyWaterLitres,
  usesTotalBodyWater,
} from './bacCalculation';

const NOW = Date.UTC(2025, 0, 1, 22, 0);
const MINUTE = 60_000;
const HOUR = 60 * MINUTE;

const man: BacProfile = { weightKg: 80, sex: 'male', heightCm: null, age: null };
const woman: BacProfile = { weightKg: 60, sex: 'female', heightCm: null, age: null };

const beer = (startedAt: number): BacDrink => ({ volumeMl: 500, abvPercent: 5, startedAt });

describe('gramsOfAlcohol', () => {
  it('weighs the ethanol of a drink', () => {
    expect(gramsOfAlcohol({ volumeMl: 500, abvPercent: 5 })).toBeCloseTo(19.725);
    expect(gramsOfAlcohol({ volumeMl: 40, abvPercent: 40 })).toBeCloseTo(12.624);
  });
});

describe('distributionFactor', () => {
  it('is the average Widmark factor of the sex without a height and an age', () => {
    expect(distributionFactor(man)).toBe(0.68);
    expect(distributionFactor(woman)).toBe(0.55);
  });

  it('is the average factor while only one of the height and the age is known', () => {
    expect(distributionFactor({ ...man, heightCm: 180 })).toBe(0.68);
    expect(distributionFactor({ ...man, age: 30 })).toBe(0.68);
    expect(usesTotalBodyWater({ heightCm: 180, age: null })).toBe(false);
  });

  it("is Watson's total body water over the water in the blood with both", () => {
    const profile: BacProfile = { weightKg: 80, sex: 'male', heightCm: 180, age: 30 };

    expect(usesTotalBodyWater(profile)).toBe(true);
    expect(totalBodyWaterLitres({ ...profile, heightCm: 180, age: 30 })).toBeCloseTo(45.82, 1);
    expect(distributionFactor(profile)).toBeCloseTo(45.82 / (0.8 * 80), 2);
  });

  it('has its own equation for women, which ignores the age', () => {
    const young: BacProfile = { weightKg: 60, sex: 'female', heightCm: 165, age: 20 };

    expect(totalBodyWaterLitres({ ...young, heightCm: 165, age: 20 })).toBeCloseTo(30.34, 1);
    expect(distributionFactor(young)).toBe(distributionFactor({ ...young, age: 60 }));
  });
});

describe('estimateBac', () => {
  it('is zero without drinks', () => {
    expect(estimateBac(man, [], NOW)).toEqual({ nowPromille: 0, peakPromille: 0, peakAt: null, soberAt: null });
  });

  it('adds a drink as grams over the weight and the factor once it is absorbed', () => {
    const drunkAt = NOW - 2 * HOUR;

    const { peakPromille } = estimateBac(man, [beer(drunkAt)], NOW);

    expect(peakPromille).toBeCloseTo(19.725 / (0.68 * 80) - (ELIMINATION_PER_HOUR * ABSORPTION_MINUTES) / 60 / 2, 1);
  });

  it('is higher for a lighter person and for a woman', () => {
    const drinks = [beer(NOW)];

    expect(estimateBac({ ...man, weightKg: 60 }, drinks, NOW).peakPromille).toBeGreaterThan(estimateBac(man, drinks, NOW).peakPromille);
    expect(estimateBac({ ...man, sex: 'female' }, drinks, NOW).peakPromille).toBeGreaterThan(estimateBac(man, drinks, NOW).peakPromille);
  });

  it('absorbs a drink over half an hour: little right after it and the peak after that', () => {
    const justNow = estimateBac(man, [beer(NOW)], NOW);
    const later = estimateBac(man, [beer(NOW - 30 * MINUTE)], NOW);

    expect(justNow.nowPromille).toBe(0);
    expect(later.nowPromille).toBeGreaterThan(0.25);
    expect(justNow.peakAt).toBe(NOW + ABSORPTION_MINUTES * MINUTE);
  });

  it('clears the blood at the elimination rate', () => {
    const at = (hoursAfter: number) => estimateBac(man, [beer(NOW - 30 * MINUTE)], NOW + hoursAfter * HOUR).nowPromille;

    expect(at(0) - at(1)).toBeCloseTo(ELIMINATION_PER_HOUR, 2);
  });

  it('says when the blood is clear', () => {
    const { nowPromille, soberAt } = estimateBac(man, [beer(NOW - 30 * MINUTE)], NOW);

    expect(soberAt).not.toBeNull();
    expect((soberAt! - NOW) / HOUR).toBeCloseTo(nowPromille / ELIMINATION_PER_HOUR, 1);
  });

  it('has no sober time once the drinks are long gone', () => {
    const estimate = estimateBac(man, [beer(NOW - 12 * HOUR)], NOW);

    expect(estimate.nowPromille).toBe(0);
    expect(estimate.soberAt).toBeNull();
    expect(estimate.peakPromille).toBeGreaterThan(0);
  });

  it('starts the next drink from zero after a gap, not from a negative value', () => {
    const alone = estimateBac(man, [beer(NOW - 30 * MINUTE)], NOW).nowPromille;

    const withOldOne = estimateBac(man, [beer(NOW - 20 * HOUR), beer(NOW - 30 * MINUTE)], NOW).nowPromille;

    expect(withOldOne).toBeCloseTo(alone, 5);
  });

  it('adds up drinks that follow each other', () => {
    const one = estimateBac(man, [beer(NOW - 30 * MINUTE)], NOW).nowPromille;
    const two = estimateBac(man, [beer(NOW - 30 * MINUTE), beer(NOW - 30 * MINUTE)], NOW).nowPromille;

    expect(two - one).toBeCloseTo(19.725 / (0.68 * 80), 1);
  });

  it('counts a drink that is not started yet as nothing now', () => {
    const estimate = estimateBac(man, [beer(NOW + HOUR)], NOW);

    expect(estimate.nowPromille).toBe(0);
    expect(estimate.soberAt).toBeGreaterThan(NOW);
  });
});
