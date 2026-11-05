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
  /** The spies you know: the other spies for a spy, every spy for the Commander (Assassin module); otherwise null. */
  fellowSpies: string[] | null;
  /** Assassin module: for the Body Guard, the players one of whom is the Commander (the Commander and the False Commander look alike); otherwise null. */
  yourCommanderCandidates: string[] | null;
  /** The caller's own character (Reverser, Defector and Assassin modules); null when none is played or the caller does not play. */
  yourCharacter: ResistanceCharacter | null;
  /** The other Defector's player id; only a Defector gets it, and only when the Defectors know each other. */
  fellowDefectorId: string | null;
  /** The switch cards flipped so far, true for Change Allegiance (Defector module). Public. */
  defectorFlips: boolean[];
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
  /** The caller's own mission card; null until they play or when they are not on the team. */
  yourMissionCard: ResistanceMissionCardKind | null;
  /** The finished missions, in order. */
  missionResults: ResistanceMissionResultDto[];
  /** The plot cards the caller holds (drawn or in hand); empty for observers and without the add-on. */
  yourPlotCards: ResistancePlotCardDto[];
  /** How many plot cards each player holds in hand (not which); public, empty without the add-on. */
  plotCardCounts: ResistancePlotCardCountDto[];
  /** Players with an Opinion Maker in play, face up for everyone; they reveal their vote before the others vote. */
  opinionMakerPlayerIds: string[];
  /** The Opinion Makers' votes, once every one of them has voted (the vote is final); empty before. */
  opinionMakerVotes: ResistanceVoteDto[];
  /** The immediate plot cards played so far, newest first; public. */
  playedPlotCards: ResistancePlayedPlotCardDto[];
  /** The identity cards played cards show the caller, until they confirm having seen them; private. */
  yourReveals: ResistanceCardRevealDto[];
  /** The team member In the Spotlight forces to play their mission card openly this mission; public, null when none. */
  openMissionPlayerId: string | null;
  /** That player's mission card, public once they played it. */
  openMissionCard: ResistanceMissionCardKind | null;
  /** LeaderWindow, ConfidenceWindow or PeekWindow: the players who confirmed they play no card; public. */
  leaderWindowPassedPlayerIds: string[];
  /** The mission cards you looked at with Keeping a Close Eye this mission; private. */
  yourPeeks: ResistanceMissionPeekDto[];
  /** Inquisitor module: the player holding the token; public, null when the module is off. */
  inquisitorPlayerId: string | null;
  /** The players who held the token, so they can't be examined any more; public. */
  pastInquisitorPlayerIds: string[];
  /** While the Inquisition waits for the examiner's confirmation: who picked a player; public. */
  inquisitionExaminerPlayerId: string | null;
  /** While the Inquisition waits for the examiner's confirmation: the player picked (the new holder of the token); public. */
  inquisitionExaminedPlayerId: string | null;
  /** The loyalty of the player you examined, until you confirm having seen it; private to the examiner. */
  yourInquisition: ResistanceInquisitionDto | null;
  /** Assassin module: the player the Assassin named as the Commander; public once they did (the game is then over). */
  assassinationTargetPlayerId: string | null;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceInquisitionDto
export type ResistanceInquisitionDto = { playerId: string; role: ResistanceRole };

// Mirrors PartyKing.Contract.V1.Games.ResistanceMissionPeekDto
export type ResistanceMissionPeekDto = { playerId: string; card: ResistanceMissionCardKind };

// Mirrors PartyKing.Contract.V1.Games.ResistancePlayedPlotCardDto
export type ResistancePlayedPlotCardDto = {
  id: number;
  type: ResistancePlotCardType;
  holderPlayerId: string;
  /** The neighbour (Overheard Conversation) or the player the card was played on. */
  targetPlayerId: string | null;
  missionNumber: number;
  isConfirmed: boolean;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceCardRevealDto
export type ResistanceCardRevealDto = {
  cardId: number;
  playerId: string;
  role: ResistanceRole;
};

// Mirrors PartyKing.Contract.V1.Games.ResistancePlotCardDto
export type ResistancePlotCardDto = {
  id: number;
  type: ResistancePlotCardType;
  /** True for a card the leader just drew and has to hand to another player. */
  toDeal: boolean;
  /** True for an Opinion Maker, which is played face up the moment it is dealt. */
  inPlay: boolean;
};

// Mirrors PartyKing.Contract.V1.Games.ResistancePlotCardCountDto
export type ResistancePlotCardCountDto = {
  playerId: string;
  count: number;
};

export type ResistancePlotCardType =
  | 'OverheardConversation'
  | 'OpinionMaker'
  | 'EstablishConfidence'
  | 'OpenUp'
  | 'NoConfidence'
  | 'KeepingCloseEye'
  | 'StrongLeader'
  | 'InTheSpotlight'
  | 'TakeResponsibility';

// Mirrors PartyKing.Contract.V1.Games.ResistanceMissionResultDto
export type ResistanceMissionResultDto = {
  missionNumber: number;
  succeeded: boolean;
  /** The Fail cards played; public, not who played them. */
  failCount: number;
  /** The Reverse cards played (Reverser module, 0 otherwise); public too. */
  reverseCount: number;
  /** The players on the mission's team (empty for a mission recorded before teams were kept). */
  teamPlayerIds: string[];
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceRulesDto
export type ResistanceRulesDto = {
  playerCounts: ResistanceCountRulesDto[];
  /** Whether the plot cards add-on is played in this game. */
  plotCardsEnabled: boolean;
  /** How many plot cards the leader draws before each mission, at the current player count. */
  plotCardsPerRound: number;
  /** Whether the Inquisitor module is played in this game. */
  inquisitorEnabled: boolean;
  /** Whether the Blind Spies variant is played in this game: the spies do not know each other. */
  blindSpiesEnabled: boolean;
  /** Whether the Reverser module is played in this game: a Reverser on each side may play a Reverse card. */
  reverserEnabled: boolean;
  /** Which sides get a Reverser while the module is played. */
  reverserSides: ResistanceReverserSides;
  /** Whether a spy Reverser and the other spies know each other; when false the Reverser spy keeps their eyes closed in the reveal. */
  reverserSpyKnowsSpies: boolean;
  /** Whether the Defector module is played in this game: a Defector on each side whose allegiance may switch from the third mission on. */
  defectorEnabled: boolean;
  /** Whether the two Defectors know each other while the module is played. */
  defectorsKnowEachOther: boolean;
  /** Whether the Assassin module is played in this game: a Commander and an Assassin, who names the Commander after the third successful mission. */
  assassinEnabled: boolean;
  /** Assassin module, optional: a Body Guard who knows the Commander (and the False Commander just as well). */
  bodyGuardEnabled: boolean;
  /** Assassin module, optional: a spy the Commander does not see. */
  deepCoverEnabled: boolean;
  /** Assassin module, optional: a spy who knows no spy and is known to none. */
  blindSpyEnabled: boolean;
  /** Assassin module, optional: a spy the Body Guard takes for the Commander. */
  falseCommanderEnabled: boolean;
};

/** Assassin module: the optional characters that can be changed together with the switch; an omitted one stays as it is. */
export type ResistanceAssassinOptions = { bodyGuard?: boolean; deepCover?: boolean; blindSpy?: boolean; falseCommander?: boolean };

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

/** A mission card; only a Reverser plays Reverse, and only a spy who is not a Reverser plays Fail. */
export type ResistanceMissionCardKind = 'Success' | 'Fail' | 'Reverse';

/** Reverser module: which sides are dealt a Reverser. */
export type ResistanceReverserSides = 'Both' | 'Resistance' | 'Spy';

/** Reverser module: the choices that can be changed together with the switch; an omitted one stays as it is. */
export type ResistanceReverserOptions = { sides?: ResistanceReverserSides; spyKnowsSpies?: boolean };

/** The special character of a player, apart from the side (Reverser, Defector and Assassin modules). */
export type ResistanceCharacter = 'Standard' | 'Reverser' | 'Defector' | 'Commander' | 'Assassin' | 'BodyGuard' | 'DeepCover' | 'BlindSpy' | 'FalseCommander';

/** DealingCards: the leader hands out the plot cards just drawn (add-on only); ResolvingCards: the immediate plot cards are played and their receivers confirm; SelectingTeam: the leader picks (the votes that rejected the last team stay public until then); Voting: the team is up for a vote; PlayingMission: the approved team plays cards; LeaderWindow: a Strong Leader is out there, so everyone confirms they play no card or its holder takes the lead; ConfidenceWindow: the team was approved while a playable card is out there, so everyone confirms they play no card, the holder of No Confidence cancels the approval or the holder of In the Spotlight names who plays openly; PeekWindow: the mission is played and its outcome is public, but a playable card is out there, so everyone confirms they play no card or the holder of Keeping a Close Eye looks at a mission card; Inquisition: the Inquisitor module, after missions 2 to 4 the holder of the token picks a player to examine and confirms having seen the loyalty; Assassination: the Assassin module, the Resistance won three missions and the Assassin names the player they take for the Commander (no winner yet); MissionResult: the outcome waits for "Dalej". */
export type ResistancePhase =
  | 'SelectingTeam'
  | 'Voting'
  | 'PlayingMission'
  | 'MissionResult'
  | 'GameOver'
  | 'DealingCards'
  | 'ResolvingCards'
  | 'LeaderWindow'
  | 'ConfidenceWindow'
  | 'PeekWindow'
  | 'Inquisition'
  | 'Assassination';

// Mirrors PartyKing.Contract.V1.Games.ResistanceVoteDto
export type ResistanceVoteDto = {
  playerId: string;
  approve: boolean;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceRevealedRoleDto
export type ResistanceRevealedRoleDto = {
  playerId: string;
  role: ResistanceRole;
  character: ResistanceCharacter;
};

// Mirrors PartyKing.Contract.V1.Games.ResistancePlayerDto
export type ResistancePlayerDto = {
  id: string;
  /** Carries the "(host)" suffix for the session host. */
  name: string;
  /** The host or an admin released the seat, so a guest may take it over from a device that is gone. */
  isSeatReleased: boolean;
  /** The seat of the session host or of an admin, which is never released. */
  isManagerSeat: boolean;
};

// Mirrors PartyKing.Contract.V1.Games.ResistanceClaimableSeatDto
export type ResistanceClaimableSeatDto = {
  id: string;
  name: string;
};

// Mirrors PartyKing.Contract.V1.Games.ResistancePresenceDto
export type ResistancePresenceDto = {
  /** The players who have the Resistance page open right now; a joined player who is not listed is offline. */
  onlinePlayerIds: string[];
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
