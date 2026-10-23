export const ROUTE_PATHS = {
  HOME: '/',
  CALLBACK: '/callback',
  SLIDESHOW: '/slideshow',
  IMAGE_UPLOADER: '/image-upload',
  ADMIN: '/admin',
  SESSION: '/s/:sessionId',
};

/** Page of a session: what is playing, the queue and who the host is. The host's player lives here too. */
export const sessionPath = (sessionId: string) => `/s/${sessionId}`;
