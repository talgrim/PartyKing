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
  endResistance: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({ useViewerRole: () => mocks.role }));
vi.mock('@/service/realtime/useSessionMusicHub', () => ({ useSessionMusicHub: vi.fn() }));
vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: {
    getResistance: mocks.getResistance,
    openResistance: mocks.openResistance,
    startResistance: mocks.startResistance,
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
});

const started = (names: string[], extra: object = {}) => ({
  ...lobby(names, true),
  startedAt: '2026-10-29T11:00:00Z',
  leaderPlayerId: 'p1',
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
    mocks.openResistance.mockReset();
    mocks.startResistance.mockReset();
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
});
