import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistanceInquisitor } from './ResistanceInquisitor';
import { ResistancePlayerMarks } from './ResistancePlayerMarks';
import { ResistanceTvBoard } from './ResistanceTvBoard';
import { missionBoardFor } from './resistanceTeams';
import { resistancePhaseDescription } from './resistancePhase';

const players = ['Ala', 'Bartek', 'Celina', 'Darek', 'Ewa'].map((name, i) => ({ id: `p${i}`, name, isSeatReleased: false, isManagerSeat: false }));

/** The holder p0 is about to examine; Ewa (p4) held the token first. */
const game = (overrides: Partial<ResistanceGameDto> = {}): ResistanceGameDto =>
  ({
    createdAt: '2026-01-01T00:00:00Z',
    isOpen: true,
    players,
    yourPlayerId: 'p0',
    startedAt: '2026-01-01T00:05:00Z',
    leaderPlayerId: 'p1',
    phase: 'Inquisition',
    missionNumber: 2,
    teamPlayerIds: [],
    votes: null,
    rejectedTeams: 0,
    leaderWindowPassedPlayerIds: [],
    missionResults: [{ missionNumber: 2, succeeded: true, failCount: 0, reverseCount: 0, teamPlayerIds: [] }],
    inquisitorPlayerId: 'p0',
    pastInquisitorPlayerIds: ['p0', 'p4'],
    inquisitionExaminerPlayerId: null,
    inquisitionExaminedPlayerId: null,
    yourInquisition: null,
    ...overrides,
  }) as unknown as ResistanceGameDto;

/** p0 picked Celina; the token is hers and p0 now looks at the result. */
const examined = (overrides: Partial<ResistanceGameDto> = {}) =>
  game({
    inquisitorPlayerId: 'p2',
    pastInquisitorPlayerIds: ['p0', 'p2', 'p4'],
    inquisitionExaminerPlayerId: 'p0',
    inquisitionExaminedPlayerId: 'p2',
    yourInquisition: { playerId: 'p2', role: 'Spy' },
    ...overrides,
  });

const viewer = { canManage: false, isAdmin: false };

describe('ResistanceInquisitor', () => {
  it('offers the holder every player who never held the token', async () => {
    const user = userEvent.setup();
    const onExamine = vi.fn().mockResolvedValue(undefined);
    render(<ResistanceInquisitor game={game()} onExamine={onExamine} onConfirm={vi.fn()} />);

    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual(['Pick a player', 'Bartek', 'Celina', 'Darek']);
    const button = screen.getByRole('button', { name: 'Examine' });
    expect(button).toBeDisabled();
    await user.selectOptions(screen.getByLabelText('Player to examine'), 'p3');
    await user.click(button);

    expect(onExamine).toHaveBeenCalledWith('p3');
  });

  it('shows the examiner the loyalty with a note that it is unverifiable, and confirms', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<ResistanceInquisitor game={examined()} onExamine={vi.fn()} onConfirm={onConfirm} />);

    expect(screen.getByText(/Celina is/)).toBeInTheDocument();
    expect(screen.getByText('Spy')).toBeInTheDocument();
    expect(screen.getByText(/nobody can check it/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Player to examine')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: "I've seen it" }));

    expect(onConfirm).toHaveBeenCalled();
  });

  it('shows the examined player, who now holds the token, nothing at all', () => {
    const { container } = render(
      <ResistanceInquisitor game={examined({ yourPlayerId: 'p2', yourInquisition: null })} onExamine={vi.fn()} onConfirm={vi.fn()} />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it.each([
    ['another player', { yourPlayerId: 'p1' }],
    ['an observer', { yourPlayerId: null }],
    ['another phase', { phase: 'MissionResult' as const }],
  ])('shows %s nothing', (_name, overrides) => {
    const { container } = render(<ResistanceInquisitor game={game(overrides)} onExamine={vi.fn()} onConfirm={vi.fn()} />);

    expect(container).toBeEmptyDOMElement();
  });
});

describe('the Inquisition in the phase line', () => {
  it('tells the holder to pick and the others who is picking', () => {
    expect(resistancePhaseDescription(game(), viewer, 2)).toContain('You hold the Inquisitor token: pick a player who never held it');
    expect(resistancePhaseDescription(game({ yourPlayerId: 'p1' }), viewer, 2)).toContain('Ala holds the Inquisitor token and picks a player to examine.');
  });

  it('says who was examined without naming the loyalty', () => {
    const own = resistancePhaseDescription(examined(), viewer, 2);
    const others = resistancePhaseDescription(examined({ yourPlayerId: 'p1', yourInquisition: null }), viewer, 2);

    expect(own).toContain('You examined Celina');
    expect(others).toContain('Ala examined Celina, who now holds the Inquisitor token');
    expect(own + others).not.toMatch(/Spy|Resistance 0/);
  });
});

describe('the token badge', () => {
  it('marks only the current holder', () => {
    const { rerender } = render(<ResistancePlayerMarks game={game({ opinionMakerPlayerIds: [], plotCardCounts: [], opinionMakerVotes: [], votedPlayerIds: [], playedMissionPlayerIds: [] })} playerId="p0" />);
    expect(screen.getByText('Inquisitor')).toBeInTheDocument();

    rerender(<ResistancePlayerMarks game={game({ opinionMakerPlayerIds: [], plotCardCounts: [], opinionMakerVotes: [], votedPlayerIds: [], playedMissionPlayerIds: [] })} playerId="p4" />);
    expect(screen.queryByText('Inquisitor')).not.toBeInTheDocument();
  });
});

describe('the token card on the TV board', () => {
  const tvGame = (overrides: Partial<ResistanceGameDto> = {}) =>
    game({
      phase: 'SelectingTeam',
      missionNumber: 3,
      opinionMakerPlayerIds: [],
      opinionMakerVotes: [],
      votedPlayerIds: [],
      playedMissionPlayerIds: [],
      plotCardCounts: [],
      ...overrides,
    });

  it('shows the Inquisitor card beside the holder only', () => {
    render(<ResistanceTvBoard game={tvGame({ inquisitorPlayerId: 'p2' })} board={missionBoardFor(5, undefined)} banner={null} yourPlayerId={null} />);

    const card = screen.getByRole('img', { name: 'Inquisitor' });
    expect(screen.getByText('Celina').closest('li')).toContainElement(card);
    expect(screen.getAllByRole('img', { name: 'Inquisitor' })).toHaveLength(1);
  });

  it('shows no card without a holder, and both cards for an Opinion Maker who holds the token', () => {
    const none = render(<ResistanceTvBoard game={tvGame({ inquisitorPlayerId: null })} board={missionBoardFor(5, undefined)} banner={null} yourPlayerId={null} />);
    expect(screen.queryByRole('img', { name: 'Inquisitor' })).not.toBeInTheDocument();
    none.unmount();

    render(
      <ResistanceTvBoard
        game={tvGame({ inquisitorPlayerId: 'p1', opinionMakerPlayerIds: ['p1'] })}
        board={missionBoardFor(5, undefined)}
        banner={null}
        yourPlayerId={null}
      />,
    );
    const seat = screen.getByText('Bartek').closest('li');
    expect(seat).toContainElement(screen.getByRole('img', { name: 'Inquisitor' }));
    expect(seat).toContainElement(screen.getByRole('img', { name: 'Opinion Maker' }));
  });
});
