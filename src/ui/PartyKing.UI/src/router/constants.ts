export const ROUTE_PATHS = {
  HOME: '/',
  CALLBACK: '/callback',
  ADMIN: '/admin',
  SESSION: '/s/:sessionId',
  SESSION_UPLOAD: '/s/:sessionId/upload',
  SESSION_GAMES: '/s/:sessionId/games',
};

/** Page of a session: what is playing, the queue and who the host is. The host's player and, for the host and admins, the slideshow live here too. */
export const sessionPath = (sessionId: string) => `/s/${sessionId}`;

/** Old address of the photo uploader; it redirects to the upload dialog on the session page. */
export const sessionUploadPath = (sessionId: string) => `/s/${sessionId}/upload`;

/** Games of a session, for the host and admins. */
export const sessionGamesPath = (sessionId: string) => `/s/${sessionId}/games`;
