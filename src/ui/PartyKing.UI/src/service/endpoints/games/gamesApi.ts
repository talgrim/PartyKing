import { axios } from '@/service/request';
import { GameDto, GameSuggestionsDto, ResistanceGameDto, ResistanceLobbyDto, RingoGameDto, RingoLobbyDto } from './types';

// The axios response interceptor unwraps `response.data`, so this resolves to the payload itself.
export const gamesApi = {
  /** Host or admin: the games available in the session; rejects with status 403 for guests. */
  getGames: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games`, { withCredentials: true }) as unknown as Promise<GameDto[]>,

  /** Host or admin: spicy "Never have I ever" statements; rejects with status 429 when asked too often. */
  getNeverHaveIEverSuggestions: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games/never-have-i-ever/suggestions`, {
      withCredentials: true,
    }) as unknown as Promise<GameSuggestionsDto>,

  /** The session's Ringo game as host, admin or player; rejects with status 404 when there is none, 403 for anyone else. */
  getRingo: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games/ringo`, { withCredentials: true }) as unknown as Promise<RingoGameDto>,

  /** Anyone in the session: whether the Ringo game is open to join and whether the caller did; 404 without a game. */
  getRingoLobby: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games/ringo/lobby`, { withCredentials: true }) as unknown as Promise<RingoLobbyDto>,

  /** Admin only: opens the Ringo game to guests. */
  openRingo: (sessionId: string) =>
    axios.post(`/sessions/${sessionId}/games/ringo/open`, null, {
      withCredentials: true,
    }) as unknown as Promise<RingoGameDto>,

  /** Joins the open game as a player; rejects with 409 when the name is taken, 400 for a bad name. The host's name is taken from Spotify. */
  joinRingo: (sessionId: string, name: string) =>
    axios.post(`/sessions/${sessionId}/games/ringo/join`, { name }, { withCredentials: true }) as unknown as Promise<void>,

  /** Host or admin: removes a player. */
  kickRingoPlayer: (sessionId: string, playerId: string) =>
    axios.delete(`/sessions/${sessionId}/games/ringo/players/${playerId}`, {
      withCredentials: true,
    }) as unknown as Promise<RingoGameDto>,

  /** Host or admin: sets the turn order; rejects with 409 when the players changed meanwhile. */
  reorderRingoPlayers: (sessionId: string, playerIds: string[]) =>
    axios.put(
      `/sessions/${sessionId}/games/ringo/players/order`,
      { playerIds },
      { withCredentials: true },
    ) as unknown as Promise<RingoGameDto>,

  /** Host or admin: starts a Ringo game; rejects with status 409 when the session already has one. */
  startRingo: (sessionId: string) =>
    axios.post(`/sessions/${sessionId}/games/ringo`, null, { withCredentials: true }) as unknown as Promise<RingoGameDto>,

  /** Flips a card (0-51), as host or admin any time and as a player on their turn; 409 while another card is pending. */
  flipRingoCard: (sessionId: string, cardId: number) =>
    axios.post(`/sessions/${sessionId}/games/ringo/cards/${cardId}/flip`, null, {
      withCredentials: true,
    }) as unknown as Promise<RingoGameDto>,

  /** Continues past the pending card, which then leaves the table, and passes the turn. Host, admin or the player on turn. */
  continueRingo: (sessionId: string) =>
    axios.post(`/sessions/${sessionId}/games/ringo/continue`, null, {
      withCredentials: true,
    }) as unknown as Promise<RingoGameDto>,

  /** Host or admin: new rules for a flipped ace, none of them among `previousRules`; rejects with status 429 when asked too often. */
  getRingoRuleSuggestions: (sessionId: string, previousRules: string[]) =>
    axios.post(
      `/sessions/${sessionId}/games/ringo/rule-suggestions`,
      { previousRules },
      { withCredentials: true },
    ) as unknown as Promise<GameSuggestionsDto>,

  /** Host or admin: ends the session's Ringo game. */
  endRingo: (sessionId: string) =>
    axios.delete(`/sessions/${sessionId}/games/ringo`, { withCredentials: true }) as unknown as Promise<void>,

  /** The session's Resistance lobby as host, admin or player (guests who unlocked the session observe once it is open); 404 when there is none or it is closed to them. */
  getResistance: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games/resistance`, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceGameDto>,

  /** Anyone in the session: whether the Resistance game is open to join and whether the caller did; 404 without a game. */
  getResistanceLobby: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games/resistance/lobby`, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceLobbyDto>,

  /** Host or admin: creates the Resistance lobby; rejects with status 409 when the session already has one. */
  createResistance: (sessionId: string) =>
    axios.post(`/sessions/${sessionId}/games/resistance`, null, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceGameDto>,

  /** Admin only: opens the Resistance game to guests. */
  openResistance: (sessionId: string) =>
    axios.post(`/sessions/${sessionId}/games/resistance/open`, null, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceGameDto>,

  /** Host or admin: starts the open game with at least 5 players (roles, first leader); 409 when it is closed, started or short of players. */
  startResistance: (sessionId: string) =>
    axios.post(`/sessions/${sessionId}/games/resistance/start`, null, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceGameDto>,

  /** The mission leader submits the team (exactly `requiredTeamSize` distinct players), which locks it; 403 for anyone else, 409 once submitted, 400 for a wrong team. */
  selectResistanceTeam: (sessionId: string, playerIds: string[]) =>
    axios.put(`/sessions/${sessionId}/games/resistance/team`, { playerIds }, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceGameDto>,

  /** A player votes on the submitted team and may change the vote until the last player has voted; 403 for non-players, 409 when no team is up for a vote. */
  voteResistanceTeam: (sessionId: string, approve: boolean) =>
    axios.put(`/sessions/${sessionId}/games/resistance/vote`, { approve }, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceGameDto>,

  /** The leader, the host or an admin moves on after a rejected team (next leader picks); 409 unless the team was rejected. */
  continueResistance: (sessionId: string) =>
    axios.post(`/sessions/${sessionId}/games/resistance/continue`, null, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceGameDto>,

  /** Joins the open game as a player; 409 when the name is taken, the lobby is full or the game started, 400 for a bad name. An admin may omit the name ("admin"). */
  joinResistance: (sessionId: string, name: string) =>
    axios.post(`/sessions/${sessionId}/games/resistance/join`, { name }, {
      withCredentials: true,
    }) as unknown as Promise<void>,

  /** Host or admin: removes a player. */
  kickResistancePlayer: (sessionId: string, playerId: string) =>
    axios.delete(`/sessions/${sessionId}/games/resistance/players/${playerId}`, {
      withCredentials: true,
    }) as unknown as Promise<ResistanceGameDto>,

  /** Host or admin: sets the seat order; rejects with 409 when the players changed meanwhile. */
  reorderResistancePlayers: (sessionId: string, playerIds: string[]) =>
    axios.put(
      `/sessions/${sessionId}/games/resistance/players/order`,
      { playerIds },
      { withCredentials: true },
    ) as unknown as Promise<ResistanceGameDto>,

  /** Host or admin: ends the session's Resistance game. */
  endResistance: (sessionId: string) =>
    axios.delete(`/sessions/${sessionId}/games/resistance`, { withCredentials: true }) as unknown as Promise<void>,
};
