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
    });

    expect(resistancePhaseDescription(started, guest, 2)).toMatch(/zgłosił\/a drużynę na pierwszą misję: Gracz 0, Gracz 3/);
  });
});
