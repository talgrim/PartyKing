// Mirrors PartyKing.Contract.V1.Demo.DemoConfigDto
export type DemoConfigDto = {
  /** This server is a demo: visitors start a throw-away session of their own. */
  enabled: boolean;
  /** ISO timestamp of the server's clock. */
  serverTime: string;
};

// Mirrors PartyKing.Contract.V1.Demo.DemoSessionDto
export type DemoSessionDto = {
  /** ISO timestamp of when the server deletes the session. */
  expiresAt: string;
  /** ISO timestamp of the server's clock, so the countdown does not trust the visitor's. */
  serverTime: string;
};

// Mirrors PartyKing.Contract.V1.Demo.DemoStartedDto
export type DemoStartedDto = {
  sessionId: string;
  /** ISO timestamp of when the server deletes the session. */
  expiresAt: string;
};
