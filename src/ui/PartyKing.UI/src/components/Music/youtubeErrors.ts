import { i18n } from '@/i18n';

// https://developers.google.com/youtube/iframe_api_reference#onError
// 101 and 150 are the same refusal. The text is looked up when asked for, so it follows the UI language.
const YOUTUBE_ERROR_KEYS = {
  2: 'youtubeErrors.e2',
  5: 'youtubeErrors.e5',
  100: 'youtubeErrors.e100',
  101: 'youtubeErrors.e101',
  150: 'youtubeErrors.e101',
} as const;

export const describeYoutubeError = (code: number | undefined): string => {
  const key = code !== undefined ? YOUTUBE_ERROR_KEYS[code as keyof typeof YOUTUBE_ERROR_KEYS] : undefined;
  return i18n.t(key ?? 'youtubeErrors.unknown');
};
