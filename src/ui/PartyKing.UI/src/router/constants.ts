export const ROUTE_PATHS = {
  HOME: '/',
  CALLBACK: '/callback',
  ADMIN: '/admin',
  SESSION: '/s/:sessionId',
  SESSION_UPLOAD: '/s/:sessionId/upload',
};

/** Page of a session: what is playing, the queue and who the host is. The host's player and, for the host and admins, the slideshow live here too. */
export const sessionPath = (sessionId: string) => `/s/${sessionId}`;

/** Where guests upload photos for the session's slideshow. */
export const sessionUploadPath = (sessionId: string) => `/s/${sessionId}/upload`;
