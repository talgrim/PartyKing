import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { Ringo } from './Ringo';

const mocks = vi.hoisted(() => ({
  role: { isHost: false, isAdmin: true },
  getRingo: vi.fn(),
  flip: vi.fn(),
  continue: vi.fn(),
  end: vi.fn(),
  suggestRules: vi.fn(),
  open: vi.fn(),
}));

// No live connection in tests; the page only passes its session id and enabled flag
vi.mock('@/service/realtime/useSessionMusicHub', () => ({ useSessionMusicHub: vi.fn() }));

vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({ useViewerRole: () => mocks.role }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: {
    getRingo: mocks.getRingo,
    flipRingoCard: mocks.flip,
    continueRingo: mocks.continue,
    endRingo: mocks.end,
    openRingo: mocks.open,
    getRingoRuleSuggestions: mocks.suggestRules,
  },
}));

const table = { isOpen: false, players: [], currentPlayerId: null, yourPlayerId: null };

const flip = (cardId: number, flippedBy = 'admin') => ({
  cardId,
  flippedAt: '2026-10-29T20:02:00Z',
  flippedBy,
});

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/games/ringo']}>
        <Routes>
          <Route path="/s/:sessionId/games/ringo" element={<Ringo />} />
          <Route path="/s/:sessionId/games" element={<p>Games page</p>} />
          <Route path="/s/:sessionId/games/ringo/join" element={<p>Join page</p>} />
          <Route path="/s/:sessionId" element={<p>Session page</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('Ringo', () => {
  beforeEach(() => {
    mocks.getRingo.mockReset();
    mocks.flip.mockReset();
    mocks.continue.mockReset();
    localStorage.clear();
    mocks.role = { isHost: false, isAdmin: true };
    mocks.end.mockReset();
    mocks.open.mockReset();
  });

  it('shows the cards still in play, and a flipped one that was continued past has left the table', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [flip(0)], pendingCardId: null, ...table });

    renderPage();

    expect(await screen.findAllByRole('button', { name: 'Face down card' })).toHaveLength(51);
    expect(screen.queryByRole('button', { name: '2♠' })).not.toBeInTheDocument();
  });

  it('flips a card on click and shows it in the middle with its effect until continued', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [], pendingCardId: null, ...table });
    mocks.flip.mockResolvedValue({ seed: 1, flips: [flip(5)], pendingCardId: 5, ...table });
    mocks.continue.mockResolvedValue({ seed: 1, flips: [flip(5)], pendingCardId: null, ...table });
    renderPage();

    await userEvent.click((await screen.findAllByRole('button', { name: 'Face down card' }))[0]);

    await waitFor(() => expect(mocks.flip).toHaveBeenCalledTimes(1));
    expect(mocks.flip.mock.calls[0][0]).toBe('session-1');
    const overlay = await screen.findByRole('dialog', { name: 'Seven of Spades' });
    expect(overlay).toHaveTextContent('Batman');

    await userEvent.click(screen.getByRole('button', { name: 'Continue' }));

    await waitFor(() => expect(mocks.continue).toHaveBeenCalledWith('session-1'));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getAllByRole('button', { name: 'Face down card' })).toHaveLength(51);
  });

  it('shows the pending card after a reload and blocks flipping other cards', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [flip(12)], pendingCardId: 12, ...table });

    renderPage();

    expect(await screen.findByRole('dialog', { name: 'Ace of Spades' })).toBeInTheDocument();
    screen.getAllByRole('button', { name: 'Face down card' }).forEach((card) => expect(card).toBeDisabled());
  });

  it('offers rule suggestions on an ace only', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [flip(12)], pendingCardId: 12, ...table });
    const { unmount } = renderPage();
    expect(await screen.findByRole('button', { name: 'Suggest rules' })).toBeInTheDocument();
    unmount();

    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [flip(5)], pendingCardId: 5, ...table });
    renderPage();

    expect(await screen.findByRole('dialog', { name: 'Seven of Spades' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Suggest rules' })).not.toBeInTheDocument();
  });

  it('shows the end of the game with the name of whoever flipped the fourth king', async () => {
    mocks.getRingo.mockResolvedValue({
      seed: 1,
      flips: [flip(11), flip(24), flip(37), flip(50, 'guest')],
      pendingCardId: 50,
      ...table,
    });
    mocks.end.mockResolvedValue(undefined);
    renderPage();

    const screenOver = await screen.findByRole('dialog', { name: 'Game over' });
    expect(screenOver).toHaveTextContent('guest flipped the fourth king: King of Clubs');
    expect(screen.queryByRole('button', { name: 'Continue' })).not.toBeInTheDocument();
    screen.getAllByRole('button', { name: 'Face down card' }).forEach((card) => expect(card).toBeDisabled());

    await userEvent.click(within(screenOver).getByRole('button', { name: 'End game' }));

    await waitFor(() => expect(mocks.end).toHaveBeenCalledWith('session-1'));
    expect(await screen.findByText('Games page')).toBeInTheDocument();
  });

  it('puts continued kings and aces on their piles', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [flip(11), flip(12), flip(25)], pendingCardId: null, ...table });

    renderPage();

    expect(await screen.findByLabelText('Kings 1/4')).toHaveTextContent('K♠');
    expect(screen.getByLabelText('Aces 2')).toHaveTextContent('A♠A♥');
  });

  it('lists the history in a drawer that is collapsed by default', async () => {
    mocks.getRingo.mockResolvedValue({
      seed: 1,
      flips: [flip(0, 'guest'), flip(12, 'admin')],
      pendingCardId: null,
      ...table,
    });
    renderPage();

    await screen.findAllByRole('button', { name: 'Face down card' });
    expect(screen.queryByRole('complementary', { name: 'History' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'History' }));

    const items = within(screen.getByRole('complementary', { name: 'History' })).getAllByRole('listitem');
    expect(items[0]).toHaveTextContent(/^\d{2}:\d{2}: admin flipped the card: Ace of Spades$/);
    expect(items[1]).toHaveTextContent(/^\d{2}:\d{2}: guest flipped the card: Two of Spades$/);
  });

  it('ends the game only after a confirmation', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [], pendingCardId: null, ...table });
    mocks.end.mockResolvedValue(undefined);
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'End game' }));
    expect(mocks.end).not.toHaveBeenCalled();

    const dialog = await screen.findByRole('dialog');
    await userEvent.click(dialog.querySelector('button.MuiButton-colorError') as HTMLElement);

    await waitFor(() => expect(mocks.end).toHaveBeenCalledWith('session-1'));
    expect(await screen.findByText('Games page')).toBeInTheDocument();
  });

  it('keeps the game when the confirmation is cancelled', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [], pendingCardId: null, ...table });
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'End game' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }));

    expect(mocks.end).not.toHaveBeenCalled();
  });

  it('goes back to the games when there is no game', async () => {
    mocks.getRingo.mockRejectedValue({ status: 404 });

    renderPage();

    expect(await screen.findByText('Games page')).toBeInTheDocument();
  });

  it('sends someone who is not a player to the join page', async () => {
    mocks.role = { isHost: false, isAdmin: false };
    mocks.getRingo.mockRejectedValue({ status: 403 });

    renderPage();

    expect(await screen.findByText('Join page')).toBeInTheDocument();
  });

  it('lets a player flip only on their turn and hides the manager controls', async () => {
    mocks.role = { isHost: false, isAdmin: false };
    const players = [
      { id: 'p1', name: 'Ola' },
      { id: 'p2', name: 'Bartek' },
    ];
    mocks.getRingo.mockResolvedValue({
      seed: 1,
      flips: [],
      pendingCardId: null,
      isOpen: true,
      players,
      currentPlayerId: 'p1',
      yourPlayerId: 'p2',
    });

    renderPage();

    const cards = await screen.findAllByRole('button', { name: 'Face down card' });
    expect(cards[0]).toBeDisabled();
    expect(screen.getByRole('list', { name: 'Players' })).toHaveTextContent('Ola');
    expect(screen.queryByRole('button', { name: 'End game' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Players' })).not.toBeInTheDocument();
  });

  it('lets an admin open the game from the board, once', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [], pendingCardId: null, ...table });
    mocks.open.mockResolvedValue({ seed: 1, flips: [], pendingCardId: null, ...table, isOpen: true });
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Open the game' }));

    await waitFor(() => expect(mocks.open).toHaveBeenCalledWith('session-1'));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Open the game' })).toBeDisabled());
  });

  it('hides "Open the game" from the host', async () => {
    mocks.role = { isHost: true, isAdmin: false };
    mocks.getRingo.mockResolvedValue({ seed: 1, flips: [], pendingCardId: null, ...table });
    renderPage();

    await screen.findAllByRole('button', { name: 'Face down card' });
    expect(screen.queryByRole('button', { name: 'Open the game' })).not.toBeInTheDocument();
  });
});
