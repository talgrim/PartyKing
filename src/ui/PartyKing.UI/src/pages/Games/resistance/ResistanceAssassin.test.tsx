import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistanceRoleCard } from '../ResistanceRoleCard';
import { ResistanceAssassin } from './ResistanceAssassin';
import { ResistancePlayerMarks } from './ResistancePlayerMarks';
import { resistancePhaseDescription } from './resistancePhase';

const players = ['Ala', 'Bartek', 'Celina', 'Darek', 'Ewa'].map((name, i) => ({ id: `p${i}`, name, isSeatReleased: false, isManagerSeat: false }));

/** The Resistance won three missions; Ala (p0) is the Assassin. */
const game = (overrides: Partial<ResistanceGameDto> = {}): ResistanceGameDto =>
  ({
    createdAt: '2026-01-01T00:00:00Z',
    isOpen: true,
    players,
    yourPlayerId: 'p0',
    yourRole: 'Spy',
    yourCharacter: 'Assassin',
    startedAt: '2026-01-01T00:05:00Z',
    leaderPlayerId: 'p1',
    phase: 'Assassination',
    missionNumber: 3,
    teamPlayerIds: [],
    votes: null,
    winner: null,
    revealedRoles: null,
    rejectedTeams: 0,
    leaderWindowPassedPlayerIds: [],
    missionResults: [1, 2, 3].map((missionNumber) => ({ missionNumber, succeeded: true, failCount: 0, reverseCount: 0, teamPlayerIds: [] })),
    assassinationTargetPlayerId: null,
    opinionMakerPlayerIds: [],
    plotCardCounts: [],
    opinionMakerVotes: [],
    votedPlayerIds: [],
    playedMissionPlayerIds: [],
    inquisitorPlayerId: null,
    ...overrides,
  }) as unknown as ResistanceGameDto;

const viewer = { canManage: false, isAdmin: false };

describe('ResistanceAssassin', () => {
  it('offers the Assassin every other player and names the pick', async () => {
    const user = userEvent.setup();
    const onAssassinate = vi.fn().mockResolvedValue(undefined);
    render(<ResistanceAssassin game={game()} onAssassinate={onAssassinate} />);

    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual(['Pick a player', 'Bartek', 'Celina', 'Darek', 'Ewa']);
    const button = screen.getByRole('button', { name: 'Name the Commander' });
    expect(button).toBeDisabled();
    await user.selectOptions(screen.getByLabelText('Player to name'), 'p2');
    await user.click(button);

    expect(onAssassinate).toHaveBeenCalledWith('p2');
  });

  it.each([
    ['another spy', { yourPlayerId: 'p4', yourCharacter: 'Standard' as const }],
    ['the Commander', { yourPlayerId: 'p2', yourRole: 'Resistance' as const, yourCharacter: 'Commander' as const }],
    ['an observer', { yourPlayerId: null, yourRole: null, yourCharacter: null }],
    ['the TV tab', { yourPlayerId: null, yourRole: null, yourCharacter: null }],
    ['another phase', { phase: 'MissionResult' as const }],
  ])('shows %s nothing', (_name, overrides) => {
    const { container } = render(<ResistanceAssassin game={game(overrides)} onAssassinate={vi.fn()} />);

    expect(container).toBeEmptyDOMElement();
  });
});

describe('the Assassination in the phase line', () => {
  it('tells everyone alike that the Assassin is choosing, without naming the viewer', () => {
    const assassin = resistancePhaseDescription(game(), viewer, 2);
    const commander = resistancePhaseDescription(game({ yourPlayerId: 'p2', yourRole: 'Resistance', yourCharacter: 'Commander' }), viewer, 2);

    expect(assassin).toContain('the Assassin is choosing the player they take for the Commander');
    expect(assassin).toBe(commander);
    expect(assassin).not.toMatch(/You are|you are/);
  });

  it('says who the Assassin named once the game is over', () => {
    const hit = game({ phase: 'GameOver', winner: 'Spy', assassinationTargetPlayerId: 'p2' });
    const missed = game({ phase: 'GameOver', winner: 'Resistance', assassinationTargetPlayerId: 'p3' });

    expect(resistancePhaseDescription(hit, viewer, 2)).toContain('the Assassin named Celina, who is the Commander');
    expect(resistancePhaseDescription(missed, viewer, 2)).toContain('the Assassin named Darek, who is not the Commander');
  });

  it('still explains a game that ended without a guess', () => {
    const failed = game({ phase: 'GameOver', winner: 'Spy', missionResults: [1, 2, 3].map((missionNumber) => ({ missionNumber, succeeded: false, failCount: 1, reverseCount: 0, teamPlayerIds: [] })) });

    expect(resistancePhaseDescription(failed, viewer, 2)).toContain('three missions failed');
  });
});

describe('ResistanceRoleCard with the Assassin module', () => {
  const sees = async (props: Partial<Parameters<typeof ResistanceRoleCard>[0]>) => {
    const user = userEvent.setup();
    render(<ResistanceRoleCard role="Resistance" players={players} fellowSpyIds={[]} {...props} />);
    await user.click(screen.getByRole('button', { name: /show|hide/i }));
  };

  it('tells the Commander so and lists every spy they know', async () => {
    await sees({ role: 'Resistance', character: 'Commander', fellowSpyIds: ['p0', 'p3'] });

    expect(screen.getByText(/You are the Commander/)).toBeInTheDocument();
    const list = screen.getByRole('list', { name: 'Spies you know' });
    expect(Array.from(list.querySelectorAll('li')).map((item) => item.textContent)).toEqual(['Ala', 'Darek']);
  });

  it('tells the Assassin so, next to the usual list of fellow spies', async () => {
    await sees({ role: 'Spy', character: 'Assassin', fellowSpyIds: ['p3'] });

    expect(screen.getByText(/You are the Assassin/)).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Fellow spies' })).toHaveTextContent('Darek');
  });

  it('shows a plain Resistance member neither character nor spies', async () => {
    await sees({ role: 'Resistance', character: 'Standard' });

    expect(screen.queryByText(/Commander/)).not.toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });
});

describe('the revealed characters', () => {
  it('badges the Commander and the Assassin once the game is over', () => {
    const over = game({
      phase: 'GameOver',
      winner: 'Resistance',
      revealedRoles: [
        { playerId: 'p0', role: 'Spy', character: 'Assassin' },
        { playerId: 'p2', role: 'Resistance', character: 'Commander' },
      ],
    });

    const { rerender } = render(<ResistancePlayerMarks game={over} playerId="p0" />);
    expect(screen.getByText('Assassin')).toBeInTheDocument();
    rerender(<ResistancePlayerMarks game={over} playerId="p2" />);
    expect(screen.getByText('Commander')).toBeInTheDocument();
  });

  it('shows no character badge during the Assassination', () => {
    render(<ResistancePlayerMarks game={game()} playerId="p2" />);

    expect(screen.queryByText('Commander')).not.toBeInTheDocument();
  });
});
