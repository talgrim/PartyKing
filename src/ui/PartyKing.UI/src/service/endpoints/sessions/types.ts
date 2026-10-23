// Mirrors PartyKing.Contract.V1.Sessions.SessionDto
export type SessionDto = {
  id: string;
  /** Short code guests can search for on the landing page. */
  code: string;
  name: string;
  /** Spotify display name of the host; null until the session is claimed. */
  hostName: string | null;
  hasHost: boolean;
};

// Mirrors PartyKing.Contract.V1.Sessions.ViewerRoleDto
export type ViewerRoleDto = {
  /** Logged in as the host of this session. */
  isHost: boolean;
  isAdmin: boolean;
};

export type UserDto = {
  id: string;
  accessToken: string;
  /** The session the login claimed. */
  sessionId: string;
};
