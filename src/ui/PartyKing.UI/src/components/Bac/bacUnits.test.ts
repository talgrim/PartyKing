import { bacUnitFor, formatBac } from './bacUnits';

describe('bacUnitFor', () => {
  it('is promille with the Polish limits in Polish', () => {
    const unit = bacUnitFor('pl');

    expect(unit.symbol).toBe('‰');
    expect(unit.limits.map((limit) => limit.promille)).toEqual([0.2, 0.5]);
  });

  it('is percent with the American limit in anything else', () => {
    const unit = bacUnitFor('en');

    expect(unit.symbol).toBe('%');
    expect(unit.limits).toEqual([{ promille: 0.8, labelKey: 'bac.limits.driving' }]);
  });
});

describe('formatBac', () => {
  it('shows promille with two decimals', () => {
    expect(formatBac(0.5, bacUnitFor('pl'), 'en')).toBe('0.50');
  });

  it('converts to percent: 0.8 promille is 0.080%', () => {
    expect(formatBac(0.8, bacUnitFor('en'), 'en')).toBe('0.080');
  });

  it('uses the decimal comma of Polish', () => {
    expect(formatBac(0.5, bacUnitFor('pl'), 'pl')).toBe('0,50');
  });
});
