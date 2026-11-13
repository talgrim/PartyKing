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

// Mirrors PartyKing.Contract.V1.Demo.MyDemoSessionDto
export type MyDemoSessionDto = {
  sessionId: string;
  name: string;
  /** ISO timestamp of when the server deletes the session. */
  expiresAt: string;
};

// Mirrors PartyKing.Contract.V1.Demo.MyDemoSessionsDto
export type MyDemoSessionsDto = {
  /** The demo sessions this browser started that have not ended, the one that ends last first. */
  sessions: MyDemoSessionDto[];
  /** ISO timestamp of the server's clock, so the countdowns do not trust the visitor's. */
  serverTime: string;
};
