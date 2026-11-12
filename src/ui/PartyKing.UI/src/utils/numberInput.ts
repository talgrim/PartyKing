/** The number in a number input's text, or null while it is empty or not a number. */
export const numberOrNull = (text: string): number | null => {
  if (text.trim() === '') return null;
  const value = Number(text);
  return Number.isFinite(value) ? value : null;
};

export type StepOptions = { step: number; min?: number; max?: number };

const decimalsOf = (value: number): number => {
  const [, fraction = ''] = String(value).split('.');
  return fraction.length;
};

/**
 * The value one step up (direction 1) or down (-1) from `value`, kept within `min` and `max`. An empty field starts at
 * its minimum. The sum is rounded to the decimals of the step, so 0.5 steps never turn into 5.499999999.
 */
export const stepNumber = (value: number | null, direction: 1 | -1, { step, min, max }: StepOptions): number => {
  const clamp = (n: number) => Math.min(max ?? Infinity, Math.max(min ?? -Infinity, n));
  if (value === null) return clamp(min ?? 0);
  const decimals = Math.max(decimalsOf(step), decimalsOf(value));
  return clamp(Number((value + direction * step).toFixed(decimals)));
};
