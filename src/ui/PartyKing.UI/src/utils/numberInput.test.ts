import { numberOrNull, stepNumber } from './numberInput';

describe('numberOrNull', () => {
  it('reads a number and nothing else', () => {
    expect(numberOrNull('12.5')).toBe(12.5);
    expect(numberOrNull('0')).toBe(0);
    expect(numberOrNull('')).toBeNull();
    expect(numberOrNull('  ')).toBeNull();
    expect(numberOrNull('abc')).toBeNull();
  });
});

describe('stepNumber', () => {
  it('goes one step up or down', () => {
    expect(stepNumber(10, 1, { step: 5 })).toBe(15);
    expect(stepNumber(10, -1, { step: 5 })).toBe(5);
  });

  it('stays within the minimum and the maximum', () => {
    expect(stepNumber(2, -1, { step: 5, min: 0 })).toBe(0);
    expect(stepNumber(98, 1, { step: 5, max: 100 })).toBe(100);
  });

  it('starts an empty field at its minimum, or at zero without one', () => {
    expect(stepNumber(null, 1, { step: 1, min: 30 })).toBe(30);
    expect(stepNumber(null, -1, { step: 1, min: 30 })).toBe(30);
    expect(stepNumber(null, 1, { step: 1 })).toBe(0);
  });

  it('does not collect floating point noise from fractional steps', () => {
    expect(stepNumber(0.1, 1, { step: 0.2 })).toBe(0.3);
    expect(stepNumber(4.5, 1, { step: 0.5 })).toBe(5);
  });
});
