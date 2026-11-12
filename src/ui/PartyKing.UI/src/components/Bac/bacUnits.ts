export type BacLimitKey = 'bac.limits.afterAlcohol' | 'bac.limits.drunk' | 'bac.limits.driving';

export type BacLimit = {
  /** The limit in promille, the unit the estimate is computed in. */
  promille: number;
  labelKey: BacLimitKey;
};

export type BacUnit = {
  symbol: string;
  /** What multiplies a value in promille to give one in this unit. */
  fromPromille: number;
  decimals: number;
  /** The legal limits that apply where the language is spoken, lowest first. */
  limits: BacLimit[];
};

/** Poland: 0.2‰ is "after alcohol" (an offence), above 0.5‰ is drunkenness (a crime). */
const PROMILLE: BacUnit = {
  symbol: '‰',
  fromPromille: 1,
  decimals: 2,
  limits: [
    { promille: 0.2, labelKey: 'bac.limits.afterAlcohol' },
    { promille: 0.5, labelKey: 'bac.limits.drunk' },
  ],
};

/** The United States: 0.08% (g/dL), which is 0.8‰. */
const PERCENT: BacUnit = {
  symbol: '%',
  fromPromille: 0.1,
  decimals: 3,
  limits: [{ promille: 0.8, labelKey: 'bac.limits.driving' }],
};

/** Polish shows promille and its limits, every other language percent and the American one. */
export const bacUnitFor = (language: string): BacUnit => (language.toLowerCase().startsWith('pl') ? PROMILLE : PERCENT);

export const formatBac = (promille: number, unit: BacUnit, language: string): string =>
  (promille * unit.fromPromille).toLocaleString(language, {
    minimumFractionDigits: unit.decimals,
    maximumFractionDigits: unit.decimals,
  });
