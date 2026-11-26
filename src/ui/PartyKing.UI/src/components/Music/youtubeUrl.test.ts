import { toNoCookieYoutubeUrl } from './youtubeUrl';

describe('toNoCookieYoutubeUrl', () => {
  it.each([
    ['https://www.youtube.com/watch?v=dQw4w9WgXcQ', 'https://www.youtube-nocookie.com/watch?v=dQw4w9WgXcQ'],
    ['https://youtube.com/watch?v=dQw4w9WgXcQ&t=30', 'https://www.youtube-nocookie.com/watch?v=dQw4w9WgXcQ&t=30'],
    ['https://m.youtube.com/watch?v=dQw4w9WgXcQ', 'https://www.youtube-nocookie.com/watch?v=dQw4w9WgXcQ'],
    ['https://music.youtube.com/watch?v=dQw4w9WgXcQ', 'https://www.youtube-nocookie.com/watch?v=dQw4w9WgXcQ'],
    ['http://www.youtube.com/shorts/dQw4w9WgXcQ', 'https://www.youtube-nocookie.com/shorts/dQw4w9WgXcQ'],
    ['https://youtu.be/dQw4w9WgXcQ', 'https://www.youtube-nocookie.com/watch?v=dQw4w9WgXcQ'],
    ['https://youtu.be/dQw4w9WgXcQ?t=30', 'https://www.youtube-nocookie.com/watch?t=30&v=dQw4w9WgXcQ'],
  ])('moves %s to the privacy-enhanced domain', (url, expected) => {
    expect(toNoCookieYoutubeUrl(url)).toBe(expected);
  });

  it('leaves a link that is already there, other sites and text that is not a link as they are', () => {
    expect(toNoCookieYoutubeUrl('https://www.youtube-nocookie.com/watch?v=dQw4w9WgXcQ')).toBe(
      'https://www.youtube-nocookie.com/watch?v=dQw4w9WgXcQ',
    );
    expect(toNoCookieYoutubeUrl('https://example.com/watch?v=dQw4w9WgXcQ')).toBe('https://example.com/watch?v=dQw4w9WgXcQ');
    expect(toNoCookieYoutubeUrl('not a url')).toBe('not a url');
  });
});
