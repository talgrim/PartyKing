export const ROUTE_PATHS = {
  HOME: '/',
  CALLBACK: '/callback',
  ADMIN: '/admin',
  SESSION: '/s/:sessionId',
  SESSION_SLIDESHOW: '/s/:sessionId/slideshow',
  SESSION_UPLOAD: '/s/:sessionId/upload',
};

/** Page of a session: what is playing, the queue and who the host is. The host's player lives here too. */
export const sessionPath = (sessionId: string) => `/s/${sessionId}`;

/** The session's slideshow, meant for a screen at the party. */
export const sessionSlideshowPath = (sessionId: string) => `/s/${sessionId}/slideshow`;

/** Where guests upload photos for the session's slideshow. */
export const sessionUploadPath = (sessionId: string) => `/s/${sessionId}/upload`;
