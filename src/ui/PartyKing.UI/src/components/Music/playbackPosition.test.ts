import { SEEK_TOLERANCE_MS, createPositionTracker } from './playbackPosition';

describe('createPositionTracker', () => {
  let time = 0;
  const tracker = () => createPositionTracker(() => time);

  beforeEach(() => {
    time = 0;
  });

  it('never calls the first position a seek', () => {
    expect(tracker().observe(90000, false)).toBe(false);
  });

  it('counts on a playing song, so ordinary progress is no seek', () => {
    const t = tracker();
    t.observe(10000, false);
    time = 5000;

    expect(t.observe(15100, false)).toBe(false);
  });

  it('notices a jump forward and backward', () => {
    const t = tracker();
    t.observe(10000, false);
    time = 1000;
    expect(t.observe(60000, false)).toBe(true);

    time = 2000;
    expect(t.observe(5000, false)).toBe(true);
  });

  it('does not count on a paused song', () => {
    const t = tracker();
    t.observe(10000, true);
    time = 30000;

    expect(t.observe(10000, false)).toBe(false);
  });

  it('allows the jitter of a report', () => {
    const t = tracker();
    t.observe(10000, false);
    time = 1000;

    expect(t.observe(11000 + SEEK_TOLERANCE_MS - 1, false)).toBe(false);
  });

  it('forgets the position on reset, so the next song is no seek', () => {
    const t = tracker();
    t.observe(200000, false);
    t.reset();

    expect(t.observe(0, false)).toBe(false);
  });
});
