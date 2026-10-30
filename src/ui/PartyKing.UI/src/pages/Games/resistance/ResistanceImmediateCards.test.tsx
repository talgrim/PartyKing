import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistancePlotLog } from './ResistancePlotLog';
import { ResistanceCloseEye } from './ResistanceCloseEye';
import { ResistanceLeaderWindow } from './ResistanceLeaderWindow';
import { ResistanceSpotlightPlayer } from './ResistanceSpotlightPlayer';
import { ResistanceTakeResponsibility } from './ResistanceTakeResponsibility';
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
    leaderWindowPassedPlayerIds: [],
    yourPeeks: [],
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

describe('Take Responsibility', () => {
  const take = { id: 9, type: 'TakeResponsibility', toDeal: false, inPlay: false } as const;
  const selecting = (overrides: Partial<ResistanceGameDto> = {}) =>
    game({ phase: 'SelectingTeam', yourPlotCards: [take], ...overrides });

  it('takes from a player without a choice when they have no Opinion Maker', async () => {
    const user = userEvent.setup();
    const onTake = vi.fn().mockResolvedValue(undefined);
    render(<ResistanceTakeResponsibility game={selecting()} onTake={onTake} />);

    await user.selectOptions(screen.getByLabelText('Któremu graczowi zabierasz kartę'), 'p3');
    await user.click(screen.getByRole('button', { name: 'Zabierz kartę' }));

    expect(onTake).toHaveBeenCalledWith(9, 'p3', null);
  });

  it('asks which card to take when the target has an Opinion Maker and cards in hand', async () => {
    const user = userEvent.setup();
    const onTake = vi.fn().mockResolvedValue(undefined);
    render(
      <ResistanceTakeResponsibility
        game={selecting({ opinionMakerPlayerIds: ['p3'], plotCardCounts: [{ playerId: 'p3', count: 2 }] })}
        onTake={onTake}
      />,
    );

    await user.selectOptions(screen.getByLabelText('Któremu graczowi zabierasz kartę'), 'p3');
    expect(screen.getByRole('button', { name: 'Zabierz kartę' })).toBeDisabled();
    await user.click(screen.getByLabelText('Losowa karta z ręki'));
    await user.click(screen.getByRole('button', { name: 'Zabierz kartę' }));

    expect(onTake).toHaveBeenCalledWith(9, 'p3', false);
  });

  it('is hidden while a team is voted on and without the card', () => {
    const voting = render(<ResistanceTakeResponsibility game={selecting({ phase: 'Voting' })} onTake={vi.fn()} />);
    expect(voting.container).toBeEmptyDOMElement();

    const none = render(<ResistanceTakeResponsibility game={selecting({ yourPlotCards: [] })} onTake={vi.fn()} />);
    expect(none.container).toBeEmptyDOMElement();
  });
});

describe('Strong Leader leader window', () => {
  const strong = { id: 5, type: 'StrongLeader', toDeal: false, inPlay: false } as const;
  const windowGame = (overrides: Partial<ResistanceGameDto> = {}) =>
    game({ phase: 'LeaderWindow', yourPlotCards: [strong], ...overrides });

  it('lets the holder take the lead', async () => {
    const user = userEvent.setup();
    const onTakeLead = vi.fn().mockResolvedValue(undefined);
    render(<ResistanceLeaderWindow game={windowGame()} onTakeLead={onTakeLead} onNoConfidence={vi.fn()} onPass={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Przejmij przywództwo' }));

    expect(onTakeLead).toHaveBeenCalledWith(5);
  });

  it('offers everybody the confirmation but the lead only to a holder who is not the leader', async () => {
    const user = userEvent.setup();
    const onPass = vi.fn().mockResolvedValue(undefined);
    render(<ResistanceLeaderWindow game={windowGame({ yourPlotCards: [] })} onTakeLead={vi.fn()} onNoConfidence={vi.fn()} onPass={onPass} />);

    expect(screen.queryByRole('button', { name: 'Przejmij przywództwo' })).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Nie zagrywam karty' }));

    expect(onPass).toHaveBeenCalled();
  });

  it('disables the confirmation once given and counts the confirmations in the phase line', () => {
    const passed = windowGame({ leaderWindowPassedPlayerIds: ['p0', 'p2'] });
    render(<ResistanceLeaderWindow game={passed} onTakeLead={vi.fn()} onNoConfidence={vi.fn()} onPass={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Potwierdzono' })).toBeDisabled();
    expect(resistancePhaseDescription(passed, { canManage: false, isAdmin: false }, 2)).toContain('Potwierdzili: 2/5');
  });

  it('lets the holder of No Confidence cancel the approval in the confidence window', async () => {
    const user = userEvent.setup();
    const onNoConfidence = vi.fn().mockResolvedValue(undefined);
    const noConfidence = { id: 7, type: 'NoConfidence', toDeal: false, inPlay: false } as const;
    const approved = windowGame({ phase: 'ConfidenceWindow', yourPlotCards: [noConfidence], teamPlayerIds: ['p0', 'p1'] });
    render(<ResistanceLeaderWindow game={approved} onTakeLead={vi.fn()} onNoConfidence={onNoConfidence} onPass={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Odrzuć zatwierdzoną drużynę' }));

    expect(onNoConfidence).toHaveBeenCalledWith(7);
    expect(resistancePhaseDescription(approved, { canManage: false, isAdmin: false }, 2)).toContain('decydują, czy zagrywają karty');
  });
});

describe('Keeping a Close Eye', () => {
  const eye = { id: 4, type: 'KeepingCloseEye', toDeal: false, inPlay: false } as const;
  const afterMission = (overrides: Partial<ResistanceGameDto> = {}) =>
    game({ phase: 'MissionResult', missionNumber: 1, teamPlayerIds: ['p0', 'p1', 'p2'], yourPlotCards: [eye], ...overrides });

  it('offers the other team members whose card nobody looked at yet', async () => {
    const user = userEvent.setup();
    const onPeek = vi.fn().mockResolvedValue(undefined);
    const looked = { id: 8, type: 'KeepingCloseEye', holderPlayerId: 'p3', targetPlayerId: 'p1', missionNumber: 1, isConfirmed: true } as const;
    render(<ResistanceCloseEye game={afterMission({ playedPlotCards: [looked] })} onPeek={onPeek} />);

    expect(screen.queryByRole('option', { name: 'Bartek' })).toBeNull();
    await user.selectOptions(screen.getByLabelText('Której karcie Misji się przyglądasz'), 'p2');
    await user.click(screen.getByRole('button', { name: 'Podejrzyj kartę' }));

    expect(onPeek).toHaveBeenCalledWith(4, 'p2');
  });

  it('shows the card that was seen and hides the picker outside the mission result', () => {
    const seen = render(<ResistanceCloseEye game={afterMission({ yourPlotCards: [], yourPeeks: [{ playerId: 'p2', success: false }] })} onPeek={vi.fn()} />);
    expect(seen.getByText('Celina zagrał/a: Porażka')).toBeInTheDocument();
    seen.unmount();

    const early = render(<ResistanceCloseEye game={afterMission({ phase: 'PlayingMission' })} onPeek={vi.fn()} />);
    expect(early.container).toBeEmptyDOMElement();
  });
});
