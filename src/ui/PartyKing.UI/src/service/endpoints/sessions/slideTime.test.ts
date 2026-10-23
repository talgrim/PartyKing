import { secondsToTimeSpan, timeSpanToSeconds } from './slideTime';

describe('slideTime', () => {
  it('formats seconds as a .NET TimeSpan', () => {
    expect(secondsToTimeSpan(5)).toBe('00:00:05');
    expect(secondsToTimeSpan(65)).toBe('00:01:05');
    expect(secondsToTimeSpan(3600)).toBe('01:00:00');
  });

  it('reads a .NET TimeSpan back into seconds', () => {
    expect(timeSpanToSeconds('00:00:20')).toBe(20);
    expect(timeSpanToSeconds('00:01:05')).toBe(65);
    expect(timeSpanToSeconds('00:00:05.5')).toBe(5.5);
  });
});
