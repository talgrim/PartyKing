export const ROUTE_PATHS = {
  HOME: '/',
  CALLBACK: '/callback',
  SLIDESHOW: '/slideshow',
  IMAGE_UPLOADER: '/image-upload',
  ADMIN: '/admin',
  SESSION: '/s/:sessionId',
  SESSION_MUSIC: '/s/:sessionId/music',
};

/** Guest page of a session: the queue and who the host is. */
export const sessionPath = (sessionId: string) => `/s/${sessionId}`;

/** Host page of a session: the player. */
export const sessionMusicPath = (sessionId: string) => `/s/${sessionId}/music`;
