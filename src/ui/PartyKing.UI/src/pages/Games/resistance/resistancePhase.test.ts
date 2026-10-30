import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { resistancePhaseDescription } from './resistancePhase';

const game = (count: number, extra: Partial<ResistanceGameDto> = {}): ResistanceGameDto => ({
  createdAt: '2026-10-29T10:00:00Z',
  isOpen: true,
  players: Array.from({ length: count }, (_, i) => ({ id: `p${i}`, name: `Gracz ${i}` })),
  yourPlayerId: null,
  maxPlayers: 10,
  startedAt: null,
  leaderPlayerId: null,
  yourRole: null,
  fellowSpies: null,
  missionNumber: 1,
  requiredTeamSize: null,
  teamPlayerIds: [],
  phase: null,
  rejectedTeams: 0,
  winner: null,
  votedPlayerIds: [],
  yourVote: null,
  votes: null,
  voteApproved: null,
  revealedRoles: null,
  requiredFails: null,
  playedMissionPlayerIds: [],
  yourMissionCard: null,
  missionResults: [],
  yourPlotCards: [],
  plotCardCounts: [],
  rules: {
    playerCounts: [5, 6, 7, 8, 9, 10].map((playerCount) => ({
      playerCount,
      spies: Math.ceil(playerCount / 3),
      teamSizes: ({ 5: [2, 3, 2, 3, 3], 6: [2, 3, 4, 3, 4], 7: [2, 3, 3, 4, 4] } as Record<number, number[]>)[playerCount] ?? [3, 4, 4, 5, 5],
      requiredFails: [1, 1, 1, playerCount >= 7 ? 2 : 1, 1],
    })),
    plotCardsEnabled: false,
    plotCardsPerRound: 1,
  },
  ...extra,
});

const guest = { canManage: false, isAdmin: false };
const host = { canManage: true, isAdmin: false };
const admin = { canManage: true, isAdmin: true };

describe('resistancePhaseDescription', () => {
  it('tells an admin to open a closed game and the host to wait for the admin', () => {
    expect(resistancePhaseDescription(game(0, { isOpen: false }), admin, 2)).toMatch(/Otwórz ją dla gości/);
    expect(resistancePhaseDescription(game(0, { isOpen: false }), host, 2)).toMatch(/admin otworzy/);
  });

  it('says how many players are missing below five', () => {
    const text = resistancePhaseDescription(game(3), guest, 2);

    expect(text).toMatch(/brakuje jeszcze 2/);
    expect(text).toMatch(/dotyczą gry w 5 osób/);
  });

  it('asks the host or admin to start at five and the others to wait', () => {
    expect(resistancePhaseDescription(game(5), host, 2)).toMatch(/kliknij „Start game”/);
    expect(resistancePhaseDescription(game(5), guest, 2)).toMatch(/Czekamy, aż host lub admin rozpocznie/);
    expect(resistancePhaseDescription(game(5), host, 2)).not.toMatch(/Potrzeba co najmniej/);
  });

  it('names the leader and the team size once started', () => {
    const started = game(5, { startedAt: '2026-10-29T11:00:00Z', leaderPlayerId: 'p1' });

    const text = resistancePhaseDescription(started, guest, 2);

    expect(text).toMatch(/Liderem jest Gracz 1/);
    expect(text).toMatch(/wybiera 2 osób/);
    expect(text).not.toMatch(/swojej karty/);
  });

  it('tells the leader they pick and a player to tap their card, without naming any role', () => {
    const started = game(5, { startedAt: '2026-10-29T11:00:00Z', leaderPlayerId: 'p1', yourPlayerId: 'p1', yourRole: 'Spy' });

    const text = resistancePhaseDescription(started, guest, 2);

    expect(text).toMatch(/Ty jesteś liderem/);
    expect(text).toMatch(/wybierz 2 osób/);
    expect(text).toMatch(/Dotknij swojej karty/);
    expect(text).not.toMatch(/szpieg|Szpieg|Ruch Oporu/);
  });

  it('takes the team size from the game when it tells one', () => {
    const started = game(8, { startedAt: '2026-10-29T11:00:00Z', leaderPlayerId: 'p1', requiredTeamSize: 3 });

    expect(resistancePhaseDescription(started, guest, 2)).toMatch(/wybiera 3 osób/);
  });

  it('names the submitted team', () => {
    const started = game(5, {
      startedAt: '2026-10-29T11:00:00Z',
      leaderPlayerId: 'p1',
      requiredTeamSize: 2,
      teamPlayerIds: ['p0', 'p3'],
      phase: 'Voting',
    });

    expect(resistancePhaseDescription(started, guest, 2)).toMatch(/zgłosił\/a drużynę na pierwszą misję: Gracz 0, Gracz 3/);
  });

  describe('voting', () => {
    const team = ['p0', 'p1'];
    const voting = (extra: Partial<ResistanceGameDto> = {}) =>
      game(5, { startedAt: 'x', leaderPlayerId: 'p1', requiredTeamSize: 2, teamPlayerIds: team, phase: 'Voting', ...extra });

    it('shows the voting progress and asks a player who has not voted yet', () => {
      const text = resistancePhaseDescription(
        voting({ yourPlayerId: 'p2', votedPlayerIds: ['p0'] }),
        guest,
        2,
      );

      expect(text).toContain('Zagłosowało 1/5.');
      expect(text).toContain('Zagłosuj');
    });

    it('tells a player who voted that the vote can still be changed', () => {
      const text = resistancePhaseDescription(voting({ yourPlayerId: 'p2', yourVote: true }), guest, 2);

      expect(text).toContain('możesz zmienić');
    });

    it('does not ask observers to vote', () => {
      expect(resistancePhaseDescription(voting(), host, 2)).not.toContain('Zagłosuj');
    });

    it('reports an approved team with the tally while the mission is played', () => {
      const votes = [true, true, true, false, false].map((approve, i) => ({ playerId: `p${i}`, approve }));
      const text = resistancePhaseDescription(
        voting({ phase: 'PlayingMission', voteApproved: true, votes, playedMissionPlayerIds: ['p0'] }),
        guest,
        2,
      );

      expect(text).toContain('została zatwierdzona (za: 3, przeciw: 2)');
      expect(text).toContain('Zagrano 1/');
      expect(text).not.toContain('nie jest jeszcze gotowa');
    });

    it('asks a team member to play a card and mentions a rule of several fails', () => {
      const text = resistancePhaseDescription(
        voting({ phase: 'PlayingMission', teamPlayerIds: ['p1'], yourPlayerId: 'p1', requiredFails: 2 }),
        guest,
        2,
      );

      expect(text).toContain('Zagraj swoją kartę misji');
      expect(text).toContain('od 2 kart Porażka');
    });

    it('reports the mission result with the score and who continues', () => {
      const text = resistancePhaseDescription(
        voting({ phase: 'MissionResult', missionResults: [{ missionNumber: 1, succeeded: false, failCount: 1 }] }),
        host,
        2,
      );

      expect(text).toContain('porażką (kart Porażka: 1)');
      expect(text).toContain('Ruch Oporu 0, Szpiedzy 1');
      expect(text).toContain('Kliknij „Dalej”');
    });

    it('names the reason the game ended', () => {
      const missions = [1, 2, 3].map((missionNumber) => ({ missionNumber, succeeded: true, failCount: 0 }));
      const text = resistancePhaseDescription(
        voting({ phase: 'GameOver', winner: 'Resistance', missionResults: missions }),
        guest,
        2,
      );

      expect(text).toContain('Ruch Oporu wygrał: trzy misje zakończyły się sukcesem (Ruch Oporu 3, Szpiedzy 0)');
    });

    it('reports the rejected team with the tally while the new leader picks', () => {
      const votes = [true, true, false, false, false].map((approve, i) => ({ playerId: `p${i}`, approve }));
      const rejected = voting({
        phase: 'SelectingTeam',
        teamPlayerIds: [],
        leaderPlayerId: 'p1',
        voteApproved: false,
        votes,
        rejectedTeams: 2,
      });

      const text = resistancePhaseDescription(rejected, guest, 2);
      expect(text).toContain('Poprzednia drużyna została odrzucona (za: 2, przeciw: 3)');
      expect(text).toContain('Odrzucone drużyny z rzędu: 2/5');
      expect(text).not.toContain('Dalej');
    });

    it('announces the spies winning when the game is over', () => {
      const text = resistancePhaseDescription(
        voting({ phase: 'GameOver', winner: 'Spy', voteApproved: false, votes: [], rejectedTeams: 5 }),
        guest,
        2,
      );

      expect(text).toContain('Szpiedzy wygrali');
    });

    it('mentions earlier rejections while the next team is picked', () => {
      const text = resistancePhaseDescription(
        game(5, { startedAt: 'x', leaderPlayerId: 'p1', phase: 'SelectingTeam', rejectedTeams: 3 }),
        guest,
        2,
      );

      expect(text).toContain('Odrzucone drużyny z rzędu: 3/5');
    });
  });

  it('tells the leader to deal the drawn plot cards and others who is dealing', () => {
    const dealing = game(5, {
      startedAt: '2026-10-29T10:05:00Z',
      phase: 'DealingCards',
      leaderPlayerId: 'p1',
      yourPlayerId: 'p1',
    });

    expect(resistancePhaseDescription(dealing, guest, 2)).toMatch(/Ty jesteś liderem.*rozdaj dobrane karty Intryg/);
    expect(resistancePhaseDescription({ ...dealing, yourPlayerId: 'p2' }, guest, 2)).toMatch(
      /Liderem jest Gracz 1.*rozdaje dobrane karty Intryg/,
    );
  });
});
