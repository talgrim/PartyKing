// https://developers.google.com/youtube/iframe_api_reference#onError
const YOUTUBE_ERRORS: Record<number, string> = {
  2: 'The video link is invalid',
  5: 'The video cannot be played in an HTML5 player',
  100: 'The video was not found or is private',
  101: 'The owner of the video does not allow it to be played outside YouTube',
  150: 'The owner of the video does not allow it to be played outside YouTube',
};

export const describeYoutubeError = (code: number | undefined): string =>
  (code !== undefined && YOUTUBE_ERRORS[code]) || 'The video could not be played';
