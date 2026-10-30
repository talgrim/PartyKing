import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistancePlotLog } from './ResistancePlotLog';
import { ResistanceSpotlightPlayer } from './ResistanceSpotlightPlayer';
import { ResistancePlotPlayer } from './ResistancePlotPlayer';
import { resistancePhaseDescription } from './resistancePhase';

const players = ['Ala', 'Bartek', 'Celina', 'Darek', 'Ewa'].map((name, i) => ({ id: `p${i}`, name }));

const game = (overrides: Partial<ResistanceGameDto> = {}): ResistanceGameDto =>
  ({
    createdAt: '2026-01-01T00:00:00Z',
    isOpen: true,
    players,
    yourPlayerId: 'p0',
    startedAt: '2026-01-01T00:00:00Z',
    leaderPlayerId: 'p1',
    missionNumber: 1,
    requiredTeamSize: 2,
    teamPlayerIds: [],
    phase: 'ResolvingCards',
    rejectedTeams: 0,
    votedPlayerIds: [],
    playedMissionPlayerIds: [],
    missionResults: [],
    yourPlotCards: [],
    plotCardCounts: [],
    opinionMakerPlayerIds: [],
    opinionMakerVotes: [],
    playedPlotCards: [],
    yourReveals: [],
    openMissionPlayerId: null,
    openMissionCard: null,
    ...overrides,
  }) as unknown as ResistanceGameDto;

describe('ResistancePlotPlayer', () => {
  it('lets Overheard Conversation be played only on the two neighbours, wrapping around the table', async () => {
    const user = userEvent.setup();
    const onPlay = vi.fn().mockResolvedValue(undefined);
    render(
      <ResistancePlotPlayer
        game={game({ yourPlotCards: [{ id: 3, type: 'OverheardConversation', toDeal: false, inPlay: false }] })}
        onPlay={onPlay}
        onConfirm={vi.fn()}
      />,
    );

    const options = screen.getAllByRole('option').map((option) => option.textContent);
    expect(options).toEqual(['Wybierz gracza', 'Ewa', 'Bartek']);

    const button = screen.getByRole('button', { name: 'Zagraj kartę' });
    expect(button).toBeDisabled();
    await user.selectOptions(screen.getByLabelText(/Na kogo zagrywasz kartę/), 'p4');
    await user.click(button);

    expect(onPlay).toHaveBeenCalledWith(3, 'p4');
  });

  it('lets Open Up be played on anybody but yourself', () => {
    render(
      <ResistancePlotPlayer
        game={game({ yourPlotCards: [{ id: 4, type: 'OpenUp', toDeal: false, inPlay: false }] })}
        onPlay={vi.fn()}
        onConfirm={vi.fn()}
      />,
    );

    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual([
      'Wybierz gracza',
      'Bartek',
      'Celina',
      'Darek',
      'Ewa',
    ]);
  });

  it('shows the identity card that was revealed and confirms having seen it', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(
      <ResistancePlotPlayer
        game={game({ yourReveals: [{ cardId: 7, playerId: 'p2', role: 'Spy' }] })}
        onPlay={vi.fn()}
        onConfirm={onConfirm}
      />,
    );

    expect(screen.getByText('Celina')).toBeInTheDocument();
    expect(screen.getByText('Szpieg')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Widziałem/am' }));

    expect(onConfirm).toHaveBeenCalledWith(7);
  });

  it('renders nothing without a card to play or an identity card to confirm', () => {
    const { container } = render(
      <ResistancePlotPlayer
        game={game({ yourPlotCards: [{ id: 1, type: 'NoConfidence', toDeal: false, inPlay: false }] })}
        onPlay={vi.fn()}
        onConfirm={vi.fn()}
      />,
    );

    expect(container).toBeEmptyDOMElement();
  });
});

describe('ResistancePlotLog', () => {
  it('lists the plays in the given order without telling which identity was seen', () => {
    render(
      <ResistancePlotLog
        players={players}
        cards={[
          { id: 2, type: 'OpenUp', holderPlayerId: 'p2', targetPlayerId: 'p3', missionNumber: 2, isConfirmed: false },
          { id: 1, type: 'OverheardConversation', holderPlayerId: 'p0', targetPlayerId: 'p4', missionNumber: 1, isConfirmed: true },
        ]}
      />,
    );

    const items = screen.getAllByRole('listitem').map((item) => item.textContent);
    expect(items[0]).toContain('Celina zagrał/a „Tylko tobie zaufam”');
    expect(items[0]).toContain('czeka na potwierdzenie');
    expect(items[1]).toContain('Ala zagrał/a „Jesteś naszym okiem i uchem”');
    expect(items[1]).not.toContain('czeka na potwierdzenie');
    expect(items[1]).not.toMatch(/Szpieg|Ruch Oporu/);
  });

  it('logs a card without a target by its holder alone', () => {
    render(
      <ResistancePlotLog
        players={players}
        cards={[{ id: 3, type: 'NoConfidence', holderPlayerId: 'p1', targetPlayerId: null, missionNumber: 1, isConfirmed: false }]}
      />,
    );

    expect(screen.getByRole('listitem').textContent).toContain('Bartek zagrał/a „Nie ufajcie mu”');
  });

  it('renders nothing before any card is played', () => {
    const { container } = render(<ResistancePlotLog players={players} cards={[]} />);

    expect(container).toBeEmptyDOMElement();
  });
});

describe('resistancePhaseDescription while the immediate cards are played', () => {
  it('names who still has to confirm what they saw', () => {
    const text = resistancePhaseDescription(
      game({
        playedPlotCards: [
          { id: 2, type: 'OpenUp', holderPlayerId: 'p2', targetPlayerId: 'p3', missionNumber: 1, isConfirmed: false },
          { id: 1, type: 'OverheardConversation', holderPlayerId: 'p0', targetPlayerId: 'p4', missionNumber: 1, isConfirmed: false },
        ],
      }),
      { canManage: false, isAdmin: false },
      2,
    );

    expect(text).toContain('karty natychmiastowe');
    expect(text).toContain('Czekamy na potwierdzenie: Darek, Ala.');
  });

  it('does not wait for a played card that shows no identity card', () => {
    const text = resistancePhaseDescription(
      game({
        playedPlotCards: [{ id: 3, type: 'NoConfidence', holderPlayerId: 'p1', targetPlayerId: null, missionNumber: 1, isConfirmed: false }],
      }),
      { canManage: false, isAdmin: false },
      2,
    );

    expect(text).not.toContain('Czekamy na potwierdzenie');
  });
});

describe('In the Spotlight', () => {
  const spotlight = { id: 7, type: 'InTheSpotlight', toDeal: false, inPlay: false } as const;
  const mission = (overrides: Partial<ResistanceGameDto> = {}) =>
    game({
      phase: 'PlayingMission',
      teamPlayerIds: ['p1', 'p2', 'p0'],
      playedMissionPlayerIds: ['p2'],
      yourPlotCards: [spotlight],
      ...overrides,
    });

  it('offers only the other team members who have not played their mission card', async () => {
    const user = userEvent.setup();
    const onPlay = vi.fn().mockResolvedValue(undefined);
    render(<ResistanceSpotlightPlayer game={mission()} onPlay={onPlay} />);

    const options = screen.getAllByRole('option').map((option) => option.textContent);
    expect(options).toEqual(['Wybierz gracza z drużyny', 'Bartek']);

    await user.selectOptions(screen.getByLabelText('Kogo zmuszasz do jawnej karty misji'), 'p1');
    await user.click(screen.getByRole('button', { name: 'Zagraj kartę' }));
    expect(onPlay).toHaveBeenCalledWith(7, 'p1');
  });

  it('renders nothing without the card or once a player is forced open', () => {
    const noCard = render(<ResistanceSpotlightPlayer game={mission({ yourPlotCards: [] })} onPlay={vi.fn()} />);
    expect(noCard.container).toBeEmptyDOMElement();

    const forced = render(<ResistanceSpotlightPlayer game={mission({ openMissionPlayerId: 'p1' })} onPlay={vi.fn()} />);
    expect(forced.container).toBeEmptyDOMElement();
  });

  it('names the forced player and then their open card in the phase line', () => {
    const viewer = { canManage: false, isAdmin: false };
    const before = resistancePhaseDescription(mission({ openMissionPlayerId: 'p1' }), viewer, 2);
    const after = resistancePhaseDescription(mission({ openMissionPlayerId: 'p1', openMissionCard: false }), viewer, 2);

    expect(before).toContain('Bartek musi zagrać kartę misji jawnie');
    expect(after).toContain('Bartek zagrał/a kartę jawnie: Porażka');
  });
});
