import { describeYoutubeError } from './youtubeErrors';

describe('describeYoutubeError', () => {
  it.each([101, 150])('says embedding is disallowed for code %i', (code) => {
    expect(describeYoutubeError(code)).toMatch(/does not allow it to be played outside YouTube/);
  });

  it('says the video is missing for code 100', () => {
    expect(describeYoutubeError(100)).toMatch(/not found or is private/);
  });

  it.each([undefined, 0, 999])('falls back to a generic message for %s', (code) => {
    expect(describeYoutubeError(code)).toBe('The video could not be played');
  });
});
