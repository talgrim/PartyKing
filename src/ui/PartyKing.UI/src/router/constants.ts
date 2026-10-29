export const ROUTE_PATHS = {
  HOME: '/',
  CALLBACK: '/callback',
  ADMIN: '/admin',
  SESSION: '/s/:sessionId',
  SESSION_UPLOAD: '/s/:sessionId/upload',
  SESSION_GAMES: '/s/:sessionId/games',
  SESSION_GAMES_RESISTANCE: '/s/:sessionId/games/resistance',
  SESSION_GAMES_RINGO: '/s/:sessionId/games/ringo',
  SESSION_GAMES_RINGO_JOIN: '/s/:sessionId/games/ringo/join',
};

/** Page of a session: what is playing, the queue and who the host is. The host's player and, for the host and admins, the slideshow live here too. */
export const sessionPath = (sessionId: string) => `/s/${sessionId}`;

/** Old address of the photo uploader; it redirects to the upload dialog on the session page. */
export const sessionUploadPath = (sessionId: string) => `/s/${sessionId}/upload`;

/** Games of a session, for the host and admins. */
export const sessionGamesPath = (sessionId: string) => `/s/${sessionId}/games`;

/** The Ringo board of a session, for the host and admins. */
export const sessionRingoPath = (sessionId: string) => `/s/${sessionId}/games/ringo`;

/** Where a guest enters a name to join the open Ringo game, and then sees the board. */
export const sessionRingoJoinPath = (sessionId: string) => `/s/${sessionId}/games/ringo/join`;

/** The Resistance board of a session. */
export const sessionResistancePath = (sessionId: string) => `/s/${sessionId}/games/resistance`;
