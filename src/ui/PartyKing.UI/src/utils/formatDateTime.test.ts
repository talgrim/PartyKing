import { formatDateTime } from './formatDateTime';

describe('formatDateTime', () => {
  it('writes the date with the month in words and the time, in the language it is given', () => {
    const iso = '2026-11-28T14:05:00Z';

    expect(formatDateTime(iso, 'en')).toContain('November');
    expect(formatDateTime(iso, 'en')).toContain('2026');
    expect(formatDateTime(iso, 'pl')).toContain('listopada');
  });
});
