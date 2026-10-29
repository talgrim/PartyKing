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

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/games/resistance']}>
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
});
