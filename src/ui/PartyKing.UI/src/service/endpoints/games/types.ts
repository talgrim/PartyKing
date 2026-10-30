// Mirrors PartyKing.Contract.V1.Games.GameDto
export type GameDto = {
  /** Stable slug identifying the game. */
  id: string;
  name: string;
  description: string;
};

// Mirrors PartyKing.Contract.V1.Games.GameSuggestionsDto
export type GameSuggestionsDto = {
  /** Statements for the game, in Polish. */
  suggestions: string[];
  /** True when the language model was unavailable and the statements come from the built-in list. */
  fromFallback: boolean;
};

// Mirrors PartyKing.Contract.V1.Games.RingoGameDto
export type RingoGameDto = {
  /** Seed of the card layout; the same seed gives the same layout on every device. */
  seed: number;
  /** The flipped cards in flip order. */
  flips: RingoFlipDto[];
  /** The last flipped card (0-51) until it is continued past; null when none waits for that. */
  pendingCardId: number | null;
  /** True once an admin opened the game to guests. */
  isOpen: boolean;
  /** The players in turn order. */
  players: RingoPlayerDto[];
  /** The player whose turn it is; null without players. */
  currentPlayerId: string | null;
  /** The caller's own player id; null when the caller is not a player. */
  yourPlayerId: string | null;
};

// Mirrors PartyKing.Contract.V1.Games.RingoPlayerDto
export type RingoPlayerDto = {
  id: string;
  /** Carries the "(host)" suffix for the session host. */
  name: string;
};

// Mirrors PartyKing.Contract.V1.Games.RingoLobbyDto
export type RingoLobbyDto = {
  /** Seed of the game, which tells one game from the next. */
  seed: number;
  isOpen: boolean;
  /** Whether the caller already joined. */
  joined: boolean;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceGameDto
export type ResistanceGameDto = {
  /** ISO timestamp. */
  createdAt: string;
  /** True once an admin opened the game to guests. */
  isOpen: boolean;
  /** The players in join/seat order. */
  players: ResistancePlayerDto[];
  /** The caller's own player id; null when the caller is not a player. */
  yourPlayerId: string | null;
  /** The lobby cap. */
  maxPlayers: number;
  /** ISO timestamp of the start; null while the game is a lobby. Once set, the players are locked. */
  startedAt: string | null;
  /** The player leading the mission; null until the game is started. */
  leaderPlayerId: string | null;
  /** The caller's own secret role; null for observers and before the start. */
  yourRole: ResistanceRole | null;
  /** The other spies' player ids; only a spy gets them, otherwise null. */
  fellowSpies: string[] | null;
  /** The mission being played, from 1. */
  missionNumber: number;
  /** How many players the leader sends on the mission; null before the start. */
  requiredTeamSize: number | null;
  /** The team the leader submitted for the mission; empty until then. Public. */
  teamPlayerIds: string[];
  /** The step of the round; null before the start. */
  phase: ResistancePhase | null;
  /** Teams rejected in a row during the mission; the fifth makes the spies win. */
  rejectedTeams: number;
  /** The winning side once the phase is GameOver. */
  winner: ResistanceRole | null;
  /** Who has voted on the team (not how) while the vote is on; everybody once it is in. Public. */
  votedPlayerIds: string[];
  /** The caller's own vote (true approves); null when they have not voted or are not a player. */
  yourVote: boolean | null;
  /** How everybody voted; only once all have voted, otherwise null. */
  votes: ResistanceVoteDto[] | null;
  /** Whether the team was approved; only once all have voted, otherwise null. */
  voteApproved: boolean | null;
  /** Every player's role; only once the game is over, otherwise null. */
  revealedRoles: ResistanceRevealedRoleDto[] | null;
  /** The numbers this game was created with (see `refreshResistanceRules`). */
  rules: ResistanceRulesDto;
  /** How many Fail cards make the current mission fail; null before the start. */
  requiredFails: number | null;
  /** Who has played a mission card (not which) while the mission is on; public. */
  playedMissionPlayerIds: string[];
  /** The caller's own mission card (true = Success); null until they play or when they are not on the team. */
  yourMissionCard: boolean | null;
  /** The finished missions, in order. */
  missionResults: ResistanceMissionResultDto[];
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceMissionResultDto
export type ResistanceMissionResultDto = {
  missionNumber: number;
  succeeded: boolean;
  failCount: number;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceRulesDto
export type ResistanceRulesDto = {
  playerCounts: ResistanceCountRulesDto[];
  /** Whether the plot cards add-on is played in this game. */
  plotCardsEnabled: boolean;
  /** How many plot cards the leader draws before each mission, at the current player count. */
  plotCardsPerRound: number;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceCountRulesDto
export type ResistanceCountRulesDto = {
  playerCount: number;
  spies: number;
  /** The team size of missions 1 to 5. */
  teamSizes: number[];
  /** The Fail cards that make missions 1 to 5 fail. */
  requiredFails: number[];
};

export type ResistanceRole = 'Resistance' | 'Spy';

/** SelectingTeam: the leader picks (the votes that rejected the last team stay public until then); Voting: the team is up for a vote; PlayingMission: the approved team plays cards; MissionResult: the outcome waits for "Dalej". */
export type ResistancePhase = 'SelectingTeam' | 'Voting' | 'PlayingMission' | 'MissionResult' | 'GameOver';

// Mirrors PartyKing.Contract.V1.Games.ResistanceVoteDto
export type ResistanceVoteDto = {
  playerId: string;
  approve: boolean;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceRevealedRoleDto
export type ResistanceRevealedRoleDto = {
  playerId: string;
  role: ResistanceRole;
};

// Mirrors PartyKing.Contract.V1.Games.ResistancePlayerDto
export type ResistancePlayerDto = {
  id: string;
  /** Carries the "(host)" suffix for the session host. */
  name: string;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceLobbyDto
export type ResistanceLobbyDto = {
  /** ISO timestamp of the game's creation, which tells one game from the next. */
  createdAt: string;
  isOpen: boolean;
  /** True once the game started; nobody can join then. */
  isStarted: boolean;
  /** Whether the caller already joined. */
  joined: boolean;
};

// Mirrors PartyKing.Contract.V1.Games.RingoFlipDto
export type RingoFlipDto = {
  /** Card id (0-51). */
  cardId: number;
  /** ISO timestamp. */
  flippedAt: string;
  flippedBy: string;
};
