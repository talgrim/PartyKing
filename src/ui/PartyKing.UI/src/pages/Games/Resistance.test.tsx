import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { Resistance } from './Resistance';

const mocks = vi.hoisted(() => ({
  role: { isHost: false, isAdmin: true },
  getResistance: vi.fn(),
  openResistance: vi.fn(),
  startResistance: vi.fn(),
  refreshResistanceRules: vi.fn(),
  setResistancePlotCards: vi.fn(),
  selectResistanceTeam: vi.fn(),
  voteResistanceTeam: vi.fn(),
  playResistanceMissionCard: vi.fn(),
  continueResistance: vi.fn(),
  restartResistance: vi.fn(),
  endResistance: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({ useViewerRole: () => mocks.role }));
vi.mock('@/service/realtime/useSessionMusicHub', () => ({ useSessionMusicHub: vi.fn() }));
vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: {
    getResistance: mocks.getResistance,
    openResistance: mocks.openResistance,
    startResistance: mocks.startResistance,
    refreshResistanceRules: mocks.refreshResistanceRules,
    setResistancePlotCards: mocks.setResistancePlotCards,
    selectResistanceTeam: mocks.selectResistanceTeam,
    voteResistanceTeam: mocks.voteResistanceTeam,
    playResistanceMissionCard: mocks.playResistanceMissionCard,
    continueResistance: mocks.continueResistance,
    restartResistance: mocks.restartResistance,
    endResistance: mocks.endResistance,
  },
}));

const lobby = (names: string[], isOpen = false) => ({
  createdAt: '2026-10-29T10:00:00Z',
  isOpen,
  players: names.map((name, i) => ({ id: `p${i}`, name })),
  yourPlayerId: null,
  maxPlayers: 10,
  startedAt: null,
  leaderPlayerId: null,
  yourRole: null,
  fellowSpies: null,
  missionNumber: 1,
  requiredTeamSize: null,
  teamPlayerIds: [] as string[],
  phase: null as string | null,
  rejectedTeams: 0,
  winner: null as string | null,
  votedPlayerIds: [] as string[],
  yourVote: null as boolean | null,
  votes: null as { playerId: string; approve: boolean }[] | null,
  voteApproved: null as boolean | null,
  revealedRoles: null as { playerId: string; role: string }[] | null,
  requiredFails: null as number | null,
  playedMissionPlayerIds: [] as string[],
  yourMissionCard: null as boolean | null,
  missionResults: [] as { missionNumber: number; succeeded: boolean; failCount: number; teamPlayerIds: string[] }[],
  yourPlotCards: [] as unknown[],
  plotCardCounts: [] as unknown[],
  opinionMakerPlayerIds: [] as string[],
  opinionMakerVotes: [] as unknown[],
  playedPlotCards: [] as unknown[],
  yourReveals: [] as unknown[],
  yourPeeks: [] as unknown[],
  leaderWindowPassedPlayerIds: [] as unknown[],
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
});

const started = (names: string[], extra: object = {}) => ({
  ...lobby(names, true),
  startedAt: '2026-10-29T11:00:00Z',
  leaderPlayerId: 'p1',
  requiredTeamSize: 2,
  phase: 'SelectingTeam',
  ...extra,
});

const renderPage = (path = '/s/session-1/games/resistance') =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/s/:sessionId/games/resistance" element={<Resistance />} />
          <Route path="/s/:sessionId/games" element={<p>Games page</p>} />
          <Route path="/s/:sessionId" element={<p>Session page</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('Resistance', () => {
  beforeEach(() => {
    mocks.role = { isHost: false, isAdmin: true };
    mocks.getResistance.mockReset();
    mocks.restartResistance.mockReset();
    mocks.openResistance.mockReset();
    mocks.startResistance.mockReset();
    mocks.refreshResistanceRules.mockReset();
    mocks.setResistancePlotCards.mockReset();
    mocks.selectResistanceTeam.mockReset();
    mocks.endResistance.mockReset();
    mocks.getResistance.mockResolvedValue(lobby([]));
    localStorage.clear();
  });

  it('shows the mission board', async () => {
    renderPage();

    expect(await screen.findByRole('img', { name: 'Mission board' })).toBeInTheDocument();
  });

  it('opens the rules from the info button', async () => {
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Rules' }));

    expect(await screen.findByText('Zasady gry Resistance')).toBeInTheDocument();
  });

  it('seats the players who joined and counts them', async () => {
    mocks.getResistance.mockResolvedValue(lobby(['Ala', 'Ola', 'Ela']));
    renderPage();

    const seats = await screen.findByRole('list', { name: 'Players' });
    expect(seats).toHaveTextContent('Ala');
    expect(seats).toHaveTextContent('Ela');
    expect(screen.getByText('3/10 graczy')).toBeInTheDocument();
    expect(screen.getByText(/Potrzeba co najmniej 5 graczy/)).toBeInTheDocument();
  });

  it('drops the under-5 notice once five players joined', async () => {
    mocks.getResistance.mockResolvedValue(lobby(['A', 'B', 'C', 'D', 'E']));
    renderPage();

    await screen.findByText('5/10 graczy');
    expect(screen.queryByText(/Potrzeba co najmniej 5 graczy/)).not.toBeInTheDocument();
  });

  it('lets an admin open the game once', async () => {
    mocks.openResistance.mockResolvedValue(lobby([], true));
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Open the game' }));

    expect(mocks.openResistance).toHaveBeenCalledWith('session-1');
    expect(await screen.findByRole('button', { name: 'Open the game' })).toBeDisabled();
  });

  it('refreshes the rules while the lobby is closed, for the host too', async () => {
    mocks.role = { isHost: true, isAdmin: false };
    mocks.refreshResistanceRules.mockResolvedValue(lobby([]));
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Refresh rules' }));

    expect(mocks.refreshResistanceRules).toHaveBeenCalledWith('session-1');
  });

  it('disables Refresh rules once the game is open and hides it after the start', async () => {
    mocks.getResistance.mockResolvedValue(lobby(['Ala'], true));
    const { unmount } = renderPage();

    expect(await screen.findByRole('button', { name: 'Refresh rules' })).toBeDisabled();
    unmount();

    mocks.getResistance.mockResolvedValue(started(['Ala', 'Ola', 'Ela', 'Ula', 'Ewa']));
    renderPage();

    await screen.findByRole('img', { name: 'Leader' });
    expect(screen.queryByRole('button', { name: 'Refresh rules' })).not.toBeInTheDocument();
  });

  it('switches the plot cards on before the game is opened', async () => {
    mocks.setResistancePlotCards.mockResolvedValue(lobby([]));
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Game settings' }));
    const dialog = await screen.findByRole('dialog', { name: 'Ustawienia gry' });
    expect(within(dialog).getByRole('region', { name: 'Karty Intryg' })).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Włącz karty Intryg' }));

    expect(mocks.setResistancePlotCards).toHaveBeenCalledWith('session-1', true);
  });

  it('shows a message when the plot cards cannot be switched', async () => {
    mocks.setResistancePlotCards.mockRejectedValue(new Error('boom'));
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Game settings' }));
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Włącz karty Intryg' }));

    expect(await screen.findByText('Nie udało się zmienić ustawienia kart Intryg.')).toBeInTheDocument();
  });

  it('keeps the game settings usable in the open lobby and disables them after the start', async () => {
    mocks.getResistance.mockResolvedValue(lobby(['Ala'], true));
    const { unmount } = renderPage();

    expect(await screen.findByRole('button', { name: 'Game settings' })).toBeEnabled();
    unmount();

    mocks.getResistance.mockResolvedValue(started(['Ala', 'Ola', 'Ela', 'Ula', 'Ewa']));
    renderPage();

    await screen.findByRole('img', { name: 'Leader' });
    const gear = screen.getByRole('button', { name: 'Game settings' });
    expect(gear).toBeDisabled();
    expect(gear).toHaveAttribute('title', 'Ustawienia są zablokowane po rozpoczęciu gry');
  });

  it('offers the game settings to a host on the phone layout without opening the controls', async () => {
    mocks.role = { isHost: true, isAdmin: false };
    renderPage();

    expect(await screen.findByRole('button', { name: 'Game settings' })).toBeEnabled();
  });

  it('gives guests no game settings', async () => {
    mocks.role = { isHost: false, isAdmin: false };
    renderPage();

    await screen.findByRole('img', { name: 'Mission board' });
    expect(screen.queryByRole('button', { name: 'Game settings' })).not.toBeInTheDocument();
  });

  it('gives the host no Open button and links back to the games', async () => {
    mocks.role = { isHost: true, isAdmin: false };
    renderPage();

    await screen.findByRole('img', { name: 'Mission board' });
    expect(screen.queryByRole('button', { name: 'Open the game' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to the games' })).toHaveAttribute('href', '/s/session-1/games');
  });

  it('lets a guest watch without any controls', async () => {
    mocks.role = { isHost: false, isAdmin: false };
    mocks.getResistance.mockResolvedValue(lobby(['Ala'], true));
    renderPage();

    await screen.findByRole('list', { name: 'Players' });
    expect(screen.queryByRole('button', { name: 'Open the game' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Players' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to the session' })).toHaveAttribute('href', '/s/session-1');
  });

  it.each([
    [true, 'Games page'],
    [false, 'Session page'],
  ])('sends everyone back when there is no lobby (manager: %s)', async (manager, page) => {
    mocks.role = { isHost: manager, isAdmin: false };
    mocks.getResistance.mockRejectedValue({ status: 404 });
    renderPage();

    expect(await screen.findByText(page)).toBeInTheDocument();
  });

  it('lets the host end the game after confirming, then returns to the games', async () => {
    mocks.role = { isHost: true, isAdmin: false };
    mocks.endResistance.mockResolvedValue(undefined);
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'End game' }));
    expect(mocks.endResistance).not.toHaveBeenCalled();
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'End game' }));

    expect(mocks.endResistance).toHaveBeenCalledWith('session-1');
    expect(await screen.findByText('Games page')).toBeInTheDocument();
  });

  it('keeps the game when the end is cancelled, and gives guests no End button', async () => {
    mocks.getResistance.mockResolvedValue(lobby(['Ala'], true));
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'End game' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(mocks.endResistance).not.toHaveBeenCalled();
  });

  it('gives guests no End button', async () => {
    mocks.role = { isHost: false, isAdmin: false };
    mocks.getResistance.mockResolvedValue(lobby(['Ala'], true));
    renderPage();

    await screen.findByRole('list', { name: 'Players' });
    expect(screen.queryByRole('button', { name: 'End game' })).not.toBeInTheDocument();
  });

  it('describes the phase above the board', async () => {
    mocks.getResistance.mockResolvedValue(lobby(['A', 'B'], true));
    renderPage();

    expect(await screen.findByText(/Czekamy na graczy/)).toBeInTheDocument();
  });

  describe('starting', () => {
    const five = ['A', 'B', 'C', 'D', 'E'];

    it('disables Start below five players and starts at five', async () => {
      mocks.getResistance.mockResolvedValue(lobby(['A', 'B', 'C', 'D'], true));
      const view = renderPage();
      expect(await screen.findByRole('button', { name: 'Start game' })).toBeDisabled();
      view.unmount();

      mocks.getResistance.mockResolvedValue(lobby(five, true));
      mocks.startResistance.mockResolvedValue(started(five));
      renderPage();
      await userEvent.click(await screen.findByRole('button', { name: 'Start game' }));

      expect(mocks.startResistance).toHaveBeenCalledWith('session-1');
      await waitFor(() => expect(screen.queryByRole('button', { name: 'Start game' })).not.toBeInTheDocument());
    });

    it('offers no Start to guests or before the game is open', async () => {
      mocks.role = { isHost: false, isAdmin: false };
      mocks.getResistance.mockResolvedValue(lobby(five, true));
      const view = renderPage();
      await screen.findByRole('list', { name: 'Players' });
      expect(screen.queryByRole('button', { name: 'Start game' })).not.toBeInTheDocument();
      view.unmount();

      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(lobby(five, false));
      renderPage();
      await screen.findByRole('list', { name: 'Players' });
      expect(screen.queryByRole('button', { name: 'Start game' })).not.toBeInTheDocument();
    });

    it('locks the player management and shows the leader once started', async () => {
      mocks.getResistance.mockResolvedValue(started(five));
      renderPage();

      await screen.findByRole('img', { name: 'Leader' });
      expect(screen.queryByRole('button', { name: 'Players' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Start game' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Open the game' })).not.toBeInTheDocument();
    });

    it('shows a player their card face-down and reveals it on a tap', async () => {
      mocks.role = { isHost: false, isAdmin: false };
      mocks.getResistance.mockResolvedValue(started(five, { yourPlayerId: 'p0', yourRole: 'Resistance' }));
      renderPage();

      const card = await screen.findByRole('button', { name: 'Pokaż swoją rolę' });
      expect(screen.queryByRole('img', { name: 'Ruch Oporu' })).not.toBeInTheDocument();

      await userEvent.click(card);
      expect(await screen.findByRole('img', { name: 'Ruch Oporu' })).toBeInTheDocument();

      await userEvent.click(screen.getByRole('button', { name: 'Ukryj swoją rolę' }));
      expect(screen.queryByRole('img', { name: 'Ruch Oporu' })).not.toBeInTheDocument();
    });

    it('shows a spy the other spies only while the card is face up', async () => {
      mocks.role = { isHost: false, isAdmin: false };
      mocks.getResistance.mockResolvedValue(
        started(five, { yourPlayerId: 'p0', yourRole: 'Spy', fellowSpies: ['p3'] }),
      );
      renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'Pokaż swoją rolę' }));

      expect(await screen.findByRole('img', { name: 'Szpieg' })).toBeInTheDocument();
      expect(within(screen.getByRole('list', { name: 'Fellow spies' })).getByText('D')).toBeInTheDocument();
    });

    it('marks which seat is yours, and shows no stray "0" for the Resistance role', async () => {
      mocks.role = { isHost: false, isAdmin: false };
      mocks.getResistance.mockResolvedValue(started(five, { yourPlayerId: 'p2', yourRole: 'Resistance' }));
      const { container } = renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'Board' }));
      const seats = await screen.findByRole('list', { name: 'Players' });
      const mine = within(seats).getAllByRole('listitem').find((item) => item.textContent?.includes('Ty'));
      expect(mine).toHaveTextContent('C');
      expect(container.textContent).not.toMatch(/^0/);
      expect(screen.queryByText('0')).not.toBeInTheDocument();
    });

    it('gives observers no card', async () => {
      mocks.role = { isHost: false, isAdmin: false };
      mocks.getResistance.mockResolvedValue(started(five));
      renderPage();

      await screen.findByRole('img', { name: 'Leader' });
      expect(screen.queryByRole('button', { name: 'Pokaż swoją rolę' })).not.toBeInTheDocument();
    });
  });
  describe('views', () => {
    const joined = (extra: object = {}) =>
      started(['A', 'B', 'C', 'D', 'E'], { yourPlayerId: 'p2', yourRole: 'Resistance', ...extra });

    beforeEach(() => {
      mocks.role = { isHost: false, isAdmin: false };
    });

    it('starts a player on their own view, without the player list', async () => {
      mocks.getResistance.mockResolvedValue(joined());
      renderPage();

      expect(await screen.findByRole('button', { name: 'Pokaż swoją rolę' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Player', pressed: true })).toBeInTheDocument();
      expect(screen.queryByRole('list', { name: 'Players' })).not.toBeInTheDocument();
      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('switches to the board, keeps the phase line and remembers the choice', async () => {
      mocks.getResistance.mockResolvedValue(joined());
      const view = renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'Board' }));

      expect(await screen.findByRole('img', { name: 'Mission board' })).toBeInTheDocument();
      expect(screen.getByRole('list', { name: 'Players' })).toBeInTheDocument();
      expect(screen.getByRole('status')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Pokaż swoją rolę' })).not.toBeInTheDocument();

      view.unmount();
      renderPage();
      expect(await screen.findByRole('list', { name: 'Players' })).toBeInTheDocument();
    });

    it('shows a player both views side by side on a wide screen, without the switch', async () => {
      const matchMedia = vi.fn().mockReturnValue({
        matches: true,
        media: '',
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
      });
      vi.stubGlobal('matchMedia', matchMedia);
      mocks.getResistance.mockResolvedValue(joined());
      renderPage();

      expect(await screen.findByRole('button', { name: 'Pokaż swoją rolę' })).toBeInTheDocument();
      expect(screen.getByRole('list', { name: 'Players' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Board' })).not.toBeInTheDocument();
      vi.unstubAllGlobals();
    });

    it('offers observers the board only', async () => {
      mocks.getResistance.mockResolvedValue(started(['A', 'B', 'C', 'D', 'E']));
      renderPage();

      expect(await screen.findByRole('list', { name: 'Players' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Player' })).not.toBeInTheDocument();
    });

    it('gives the TV tab no role, no "Ty" and no switch, even for a host who plays', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(joined());
      renderPage('/s/session-1/games/resistance?tv=1');

      const seats = await screen.findByRole('list', { name: 'Players' });
      expect(within(seats).queryByText('Ty')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Pokaż swoją rolę' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Board' })).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'End game' })).toBeInTheDocument();
    });

    it('keeps the manager controls of a playing host collapsed until asked for', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(
        joined({ startedAt: null, leaderPlayerId: null, yourRole: null, isOpen: true }),
      );
      renderPage();

      const toggle = await screen.findByRole('button', { name: 'Controls' });
      expect(toggle).toHaveAttribute('aria-expanded', 'false');
      expect(screen.queryByRole('button', { name: 'End game' })).not.toBeInTheDocument();

      await userEvent.click(toggle);
      expect(toggle).toHaveAttribute('aria-expanded', 'true');
      expect(screen.getByRole('link', { name: 'Open TV view' })).toHaveAttribute(
        'href',
        '/s/session-1/games/resistance?tv=1',
      );
      expect(screen.getByRole('button', { name: 'Start game' })).toBeEnabled();
      expect(screen.getByRole('button', { name: 'Players' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'End game' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Open the game' })).not.toBeInTheDocument();

      await userEvent.click(toggle);
      expect(screen.queryByRole('button', { name: 'End game' })).not.toBeInTheDocument();
    });

    it('offers guests no controls', async () => {
      mocks.getResistance.mockResolvedValue(joined());
      renderPage();

      await screen.findByRole('button', { name: 'Pokaż swoją rolę' });
      expect(screen.queryByRole('button', { name: 'Controls' })).not.toBeInTheDocument();
    });

    it('ignores the TV flag for a guest and keeps the TV tab out of the saved choice', async () => {
      mocks.getResistance.mockResolvedValue(joined());
      renderPage('/s/session-1/games/resistance?tv=1');

      expect(await screen.findByRole('button', { name: 'Pokaż swoją rolę' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'End game' })).not.toBeInTheDocument();
    });
  });
  describe('team selection', () => {
    const five = ['A', 'B', 'C', 'D', 'E'];
    const leading = (extra: object = {}) =>
      started(five, { yourPlayerId: 'p1', yourRole: 'Resistance', ...extra });

    beforeEach(() => {
      mocks.role = { isHost: false, isAdmin: false };
    });

    it('gives the leader a picker that needs exactly the required number of players', async () => {
      mocks.getResistance.mockResolvedValue(leading());
      renderPage();

      const submit = await screen.findByRole('button', { name: 'Zgłoś drużynę do głosowania' });
      expect(submit).toBeDisabled();

      await userEvent.click(screen.getByRole('checkbox', { name: 'A' }));
      expect(submit).toBeDisabled();
      await userEvent.click(screen.getByRole('checkbox', { name: 'B' }));

      expect(submit).toBeEnabled();
      expect(screen.getByRole('checkbox', { name: 'C' })).toBeDisabled();
    });

    it('keeps the picks local and sends them only on submit', async () => {
      mocks.getResistance.mockResolvedValue(leading());
      mocks.selectResistanceTeam.mockResolvedValue(leading({ teamPlayerIds: ['p0', 'p1'], phase: 'Voting' }));
      renderPage();

      await userEvent.click(await screen.findByRole('checkbox', { name: 'A' }));
      await userEvent.click(screen.getByRole('checkbox', { name: 'B' }));
      expect(mocks.selectResistanceTeam).not.toHaveBeenCalled();

      await userEvent.click(screen.getByRole('button', { name: 'Zgłoś drużynę do głosowania' }));

      expect(mocks.selectResistanceTeam).toHaveBeenCalledWith('session-1', ['p0', 'p1']);
      await waitFor(() => expect(screen.queryByRole('checkbox', { name: 'A' })).not.toBeInTheDocument());
    });

    it('shows the picker to the leader only, and not once a team was submitted', async () => {
      mocks.getResistance.mockResolvedValue(started(five, { yourPlayerId: 'p2', yourRole: 'Resistance' }));
      const view = renderPage();
      await screen.findByRole('button', { name: 'Pokaż swoją rolę' });
      expect(screen.queryByRole('checkbox', { name: 'A' })).not.toBeInTheDocument();
      view.unmount();

      mocks.getResistance.mockResolvedValue(leading({ teamPlayerIds: ['p0', 'p1'], phase: 'Voting' }));
      renderPage();
      await screen.findByRole('button', { name: 'Pokaż swoją rolę' });
      expect(screen.queryByRole('checkbox', { name: 'A' })).not.toBeInTheDocument();
    });

    it('hides the picker in the TV tab even for the leader', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(leading());
      renderPage('/s/session-1/games/resistance?tv=1');

      await screen.findByRole('img', { name: 'Mission board' });
      expect(screen.queryByRole('checkbox', { name: 'A' })).not.toBeInTheDocument();
    });

    it('shows the submitted team as cards under the board in the TV view', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(started(five, { teamPlayerIds: ['p0', 'p3'], phase: 'Voting' }));
      renderPage('/s/session-1/games/resistance?tv=1');

      const team = await screen.findByRole('region', { name: 'Team' });
      expect(within(team).getAllByRole('listitem').map((item) => item.getAttribute('title'))).toEqual(['A', 'D']);
      expect(within(team).getByText('Drużyna na misję 1')).toBeInTheDocument();
    });

    it('shows no team cards before a team was submitted', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(started(five));
      renderPage('/s/session-1/games/resistance?tv=1');

      await screen.findByRole('img', { name: 'Mission board' });
      expect(screen.queryByRole('region', { name: 'Team' })).not.toBeInTheDocument();
    });

    it('shows the submitted team to everyone on the board', async () => {
      mocks.getResistance.mockResolvedValue(started(five, { teamPlayerIds: ['p0', 'p3'], phase: 'Voting' }));
      renderPage();

      const list = await screen.findByRole('list', { name: 'Players' });
      expect(within(list).getAllByText('W drużynie')).toHaveLength(2);
    });
  });

  describe('voting', () => {
    const five = ['A', 'B', 'C', 'D', 'E'];
    const voting = (extra: object = {}) =>
      started(five, {
        yourPlayerId: 'p2',
        yourRole: 'Resistance',
        teamPlayerIds: ['p0', 'p1'],
        phase: 'Voting',
        ...extra,
      });
    const rejected = (extra: object = {}) =>
      voting({
        phase: 'SelectingTeam',
        teamPlayerIds: [],
        leaderPlayerId: 'p2',
        voteApproved: false,
        rejectedTeams: 1,
        votedPlayerIds: ['p0', 'p1', 'p2', 'p3', 'p4'],
        votes: [
          { playerId: 'p0', approve: true },
          { playerId: 'p1', approve: false },
          { playerId: 'p2', approve: false },
          { playerId: 'p3', approve: false },
          { playerId: 'p4', approve: true },
        ],
        ...extra,
      });

    beforeEach(() => {
      mocks.role = { isHost: false, isAdmin: false };
    });

    it('gives a player the vote buttons, with their current choice pressed', async () => {
      mocks.getResistance.mockResolvedValue(voting({ yourVote: false, votedPlayerIds: ['p2'] }));
      renderPage();

      expect(await screen.findByRole('button', { name: 'Odrzuć' })).toHaveAttribute('aria-pressed', 'true');
      expect(screen.getByRole('button', { name: 'Zatwierdź' })).toHaveAttribute('aria-pressed', 'false');
    });

    it('sends the vote and shows the answer', async () => {
      mocks.getResistance.mockResolvedValue(voting());
      mocks.voteResistanceTeam.mockResolvedValue(voting({ yourVote: true, votedPlayerIds: ['p2'] }));
      renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'Zatwierdź' }));

      expect(mocks.voteResistanceTeam).toHaveBeenCalledWith('session-1', true);
      await waitFor(() => expect(screen.getByRole('button', { name: 'Zatwierdź' })).toHaveAttribute('aria-pressed', 'true'));
    });

    it('lets the vote be changed after it was cast', async () => {
      mocks.getResistance.mockResolvedValue(voting({ yourVote: true, votedPlayerIds: ['p2'] }));
      mocks.voteResistanceTeam.mockResolvedValue(voting({ yourVote: false, votedPlayerIds: ['p2'] }));
      renderPage();

      await userEvent.click(await screen.findByRole('button', { name: 'Odrzuć' }));

      expect(mocks.voteResistanceTeam).toHaveBeenCalledWith('session-1', false);
    });

    it('gives observers and the TV tab no vote buttons', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(voting({ yourPlayerId: null, yourRole: null }));
      const view = renderPage();
      await screen.findByRole('img', { name: 'Mission board' });
      expect(screen.queryByRole('button', { name: 'Zatwierdź' })).not.toBeInTheDocument();
      view.unmount();

      mocks.getResistance.mockResolvedValue(voting({ yourVote: true }));
      renderPage('/s/session-1/games/resistance?tv=1');
      await screen.findByRole('img', { name: 'Mission board' });
      expect(screen.queryByRole('button', { name: 'Zatwierdź' })).not.toBeInTheDocument();
    });

    it('marks who has voted without showing how', async () => {
      mocks.getResistance.mockResolvedValue(voting({ yourPlayerId: null, yourRole: null, votedPlayerIds: ['p0', 'p3'] }));
      renderPage();

      const list = await screen.findByRole('list', { name: 'Players' });
      expect(within(list).getAllByText('Zagłosował/a')).toHaveLength(2);
      expect(within(list).queryByText('Za')).not.toBeInTheDocument();
      expect(within(list).queryByText('Przeciw')).not.toBeInTheDocument();
    });

    it('shows how everybody voted once the votes are public', async () => {
      mocks.getResistance.mockResolvedValue(rejected({ yourPlayerId: null, yourRole: null }));
      renderPage();

      const list = await screen.findByRole('list', { name: 'Players' });
      expect(within(list).getAllByText('Za')).toHaveLength(2);
      expect(within(list).getAllByText('Przeciw')).toHaveLength(3);
      expect(screen.getByText(/została odrzucona \(za: 2, przeciw: 3\)/)).toBeInTheDocument();
    });

    it('shows each voter\'s card with their name next to the team on the TV view', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(rejected({ yourPlayerId: null, yourRole: null }));
      renderPage('/s/session-1/games/resistance?tv=1');

      const votes = await screen.findByRole('region', { name: 'Votes' });
      expect(within(votes).getAllByRole('img', { name: 'Za' })).toHaveLength(2);
      expect(within(votes).getAllByRole('img', { name: 'Przeciw' })).toHaveLength(3);
      expect(within(votes).getByText('A')).toBeInTheDocument();
      expect(screen.queryByRole('region', { name: 'Team' })).not.toBeInTheDocument();
    });

    it('keeps the votes on the TV view while the mission is played and drops them once it is over', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(
        rejected({ yourPlayerId: null, yourRole: null, voteApproved: true, phase: 'PlayingMission', teamPlayerIds: ['p0', 'p1'] }),
      );
      const view = renderPage('/s/session-1/games/resistance?tv=1');
      expect(await screen.findByRole('region', { name: 'Votes' })).toBeInTheDocument();
      view.unmount();

      mocks.getResistance.mockResolvedValue(
        rejected({
          yourPlayerId: null,
          yourRole: null,
          voteApproved: true,
          phase: 'MissionResult',
          teamPlayerIds: ['p0', 'p1'],
          missionResults: [{ missionNumber: 1, succeeded: false, failCount: 1, teamPlayerIds: [] }],
        }),
      );
      renderPage('/s/session-1/games/resistance?tv=1');

      const cards = await screen.findByRole('region', { name: 'Mission cards' });
      expect(within(cards).getAllByRole('img', { name: 'Porażka' })).toHaveLength(1);
      expect(within(cards).getAllByRole('img', { name: 'Sukces' })).toHaveLength(1);
      expect(within(cards).queryByText('A')).not.toBeInTheDocument();
      expect(screen.queryByRole('region', { name: 'Votes' })).not.toBeInTheDocument();
    });

    it('shows no vote cards on the TV view until the votes are public', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(voting({ yourPlayerId: null, yourRole: null, votedPlayerIds: ['p0'] }));
      renderPage('/s/session-1/games/resistance?tv=1');

      await screen.findByRole('region', { name: 'Team' });
      expect(screen.queryByRole('region', { name: 'Votes' })).not.toBeInTheDocument();
    });

    it('has no "Dalej" after a rejected team: the next leader just picks', async () => {
      mocks.getResistance.mockResolvedValue(rejected({ yourPlayerId: 'p2', leaderPlayerId: 'p2' }));
      renderPage();

      expect(await screen.findByRole('button', { name: 'Zgłoś drużynę do głosowania' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Dalej' })).not.toBeInTheDocument();
    });

    it('has no "Dalej" while the approved team plays its mission', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(
        rejected({ yourPlayerId: null, yourRole: null, voteApproved: true, rejectedTeams: 0, phase: 'PlayingMission' }),
      );
      renderPage('/s/session-1/games/resistance?tv=1');

      await screen.findByRole('img', { name: 'Mission board' });
      expect(screen.queryByRole('button', { name: 'Dalej' })).not.toBeInTheDocument();
    });

    describe('mission', () => {
      const playing = (extra: object = {}) =>
        voting({ phase: 'PlayingMission', voteApproved: true, requiredFails: 1, yourPlayerId: 'p0', ...extra });

      it('gives a team member the cards, with Fail disabled for the Resistance', async () => {
        mocks.getResistance.mockResolvedValue(playing({ yourRole: 'Resistance' }));
        renderPage();

        expect(await screen.findByRole('button', { name: 'Sukces' })).toBeEnabled();
        expect(screen.getByRole('button', { name: 'Porażka' })).toHaveAttribute('aria-disabled', 'true');
      });

      it('sends the card a spy plays', async () => {
        mocks.getResistance.mockResolvedValue(playing({ yourRole: 'Spy', fellowSpies: [] }));
        mocks.playResistanceMissionCard.mockResolvedValue(playing({ yourRole: 'Spy', yourMissionCard: false }));
        renderPage();

        await userEvent.click(await screen.findByRole('button', { name: 'Porażka' }));

        expect(mocks.playResistanceMissionCard).toHaveBeenCalledWith('session-1', false);
        expect(await screen.findByText(/Zagrano kartę: Porażka/)).toBeInTheDocument();
      });

      it('shows no cards to a player outside the team', async () => {
        mocks.getResistance.mockResolvedValue(playing({ yourPlayerId: 'p3' }));
        renderPage();

        await screen.findByRole('img', { name: 'Mission board' });
        expect(screen.queryByRole('button', { name: 'Sukces' })).not.toBeInTheDocument();
      });

      it('lets the leader continue once the mission is over', async () => {
        mocks.getResistance.mockResolvedValue(
          playing({
            phase: 'MissionResult',
            yourPlayerId: 'p1',
            leaderPlayerId: 'p1',
            missionResults: [{ missionNumber: 1, succeeded: true, failCount: 0, teamPlayerIds: [] }],
          }),
        );
        mocks.continueResistance.mockResolvedValue(started(five, { yourPlayerId: 'p1', missionNumber: 2 }));
        renderPage();

        await userEvent.click(await screen.findByRole('button', { name: 'Dalej' }));

        expect(mocks.continueResistance).toHaveBeenCalledWith('session-1');
      });
    });

    it('shows the picker again for the next leader after a rejection', async () => {
      mocks.getResistance.mockResolvedValue(
        started(five, { yourPlayerId: 'p2', yourRole: 'Resistance', leaderPlayerId: 'p2', rejectedTeams: 1 }),
      );
      renderPage();

      expect(await screen.findByRole('button', { name: 'Zgłoś drużynę do głosowania' })).toBeInTheDocument();
      expect(screen.getByText(/Odrzucone drużyny z rzędu: 1\/5/)).toBeInTheDocument();
    });

    it('announces the spies winning and reveals every role when the game is over', async () => {
      mocks.getResistance.mockResolvedValue(
        rejected({
          yourPlayerId: null,
          yourRole: null,
          phase: 'GameOver',
          winner: 'Spy',
          rejectedTeams: 5,
          revealedRoles: [
            { playerId: 'p0', role: 'Spy' },
            { playerId: 'p1', role: 'Resistance' },
            { playerId: 'p2', role: 'Resistance' },
            { playerId: 'p3', role: 'Spy' },
            { playerId: 'p4', role: 'Resistance' },
          ],
        }),
      );
      renderPage();

      const list = await screen.findByRole('list', { name: 'Players' });
      expect(within(list).getAllByText('Szpieg')).toHaveLength(2);
      expect(within(list).getAllByText('Ruch Oporu')).toHaveLength(3);
      expect(screen.getByText(/Szpiedzy wygrali/)).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Dalej' })).not.toBeInTheDocument();
    });

    it('lets a manager restart the game once it is over', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(
        rejected({ yourPlayerId: null, yourRole: null, phase: 'GameOver', winner: 'Spy', rejectedTeams: 5, revealedRoles: [] }),
      );
      mocks.restartResistance.mockResolvedValue(lobby(five, true));
      renderPage('/s/session-1/games/resistance?tv=1');

      await userEvent.click(await screen.findByRole('button', { name: 'Restart game' }));

      expect(mocks.restartResistance).toHaveBeenCalledWith('session-1');
      await waitFor(() => expect(screen.queryByRole('button', { name: 'Restart game' })).not.toBeInTheDocument());
      expect(await screen.findByRole('button', { name: 'Start game' }, { timeout: 5000 })).toBeEnabled();
      expect(await screen.findByRole('button', { name: 'Game settings' }, { timeout: 5000 })).toBeEnabled();
    });

    it('offers no restart while the game is on', async () => {
      mocks.role = { isHost: true, isAdmin: false };
      mocks.getResistance.mockResolvedValue(started(five, { yourPlayerId: null, yourRole: null }));
      renderPage('/s/session-1/games/resistance?tv=1');

      await screen.findByRole('button', { name: 'End game' });
      expect(screen.queryByRole('button', { name: 'Restart game' })).not.toBeInTheDocument();
    });
  });
});
