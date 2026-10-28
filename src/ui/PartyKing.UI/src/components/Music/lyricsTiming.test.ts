import { activeLineIndex, formatOffset } from './lyricsTiming';

const lines = [
  { timeMs: 1000, text: 'one' },
  { timeMs: 5000, text: 'two' },
  { timeMs: 9000, text: 'three' },
];

describe('activeLineIndex', () => {
  it('is -1 before the first line starts', () => {
    expect(activeLineIndex(lines, 0)).toBe(-1);
    expect(activeLineIndex(lines, 999)).toBe(-1);
  });

  it('is the line that has started last', () => {
    expect(activeLineIndex(lines, 1000)).toBe(0);
    expect(activeLineIndex(lines, 4999)).toBe(0);
    expect(activeLineIndex(lines, 5000)).toBe(1);
    expect(activeLineIndex(lines, 600000)).toBe(2);
  });

  it('is -1 without lines', () => {
    expect(activeLineIndex([], 5000)).toBe(-1);
  });
});

describe('formatOffset', () => {
  it('shows the sign and one decimal', () => {
    expect(formatOffset(0)).toBe('0.0 s');
    expect(formatOffset(500)).toBe('+0.5 s');
    expect(formatOffset(-1500)).toBe('−1.5 s');
  });
});
