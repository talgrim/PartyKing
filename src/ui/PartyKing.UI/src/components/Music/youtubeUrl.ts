const YOUTUBE_HOSTS = new Set(['youtube.com', 'www.youtube.com', 'm.youtube.com', 'music.youtube.com']);
const SHORT_HOSTS = new Set(['youtu.be', 'www.youtu.be']);

/**
 * The same video on the privacy-enhanced domain: the embed there sets no cookie until the viewer plays it
 * (`youtube-video-element` picks the `youtube-nocookie.com` embed when the source contains "-nocookie"). Anything that
 * is not a YouTube link is returned as it is.
 */
export const toNoCookieYoutubeUrl = (url: string): string => {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return url;
  }

  if (SHORT_HOSTS.has(parsed.hostname)) {
    // youtu.be/<id>?t=30 is watch?v=<id>&t=30
    const query = new URLSearchParams(parsed.search);
    query.set('v', parsed.pathname.slice(1));

    return `https://www.youtube-nocookie.com/watch?${query.toString()}`;
  }

  if (!YOUTUBE_HOSTS.has(parsed.hostname)) {
    return url;
  }

  parsed.protocol = 'https:';
  parsed.hostname = 'www.youtube-nocookie.com';

  return parsed.toString();
};
