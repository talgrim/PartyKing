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

// Mirrors PartyKing.Contract.V1.Sessions.JoinTokenDto
export type JoinTokenDto = {
  /** Signed token that unlocks the session without its password. */
  token: string;
  /** ISO timestamp after which the token stops working. */
  expiresAt: string;
};

export type UserDto = {
  id: string;
  accessToken: string;
  /** The session the login claimed. */
  sessionId: string;
};

// Mirrors PartyKing.Contract.V1.Slideshow.SlideshowSettingsDto
export type SlideshowSettingsDto = {
  /** Start over after the last photo. */
  autoRepeat: boolean;
  /** Show photos that were already presented again. */
  startFromBeginning: boolean;
  /** How long a photo stays, as a .NET TimeSpan ("00:00:20"). */
  slideTime: string;
};
