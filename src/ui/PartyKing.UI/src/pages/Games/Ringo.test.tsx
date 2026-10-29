import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { Ringo } from './Ringo';

const mocks = vi.hoisted(() => ({ getRingo: vi.fn(), flip: vi.fn(), end: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getRingo: mocks.getRingo, flipRingoCard: mocks.flip, endRingo: mocks.end },
}));

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/games/ringo']}>
        <Routes>
          <Route path="/s/:sessionId/games/ringo" element={<Ringo />} />
          <Route path="/s/:sessionId/games" element={<p>Games page</p>} />
          <Route path="/s/:sessionId" element={<p>Session page</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('Ringo', () => {
  beforeEach(() => {
    mocks.getRingo.mockReset();
    mocks.flip.mockReset();
    mocks.end.mockReset();
  });

  it('shows all 52 cards, the flipped ones face up', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flippedCards: [0] });

    renderPage();

    expect(await screen.findByRole('button', { name: '2♠' })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Face down card' })).toHaveLength(51);
  });

  it('flips a card on click', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flippedCards: [] });
    mocks.flip.mockResolvedValue({ seed: 1, flippedCards: [5] });
    renderPage();

    await userEvent.click((await screen.findAllByRole('button', { name: 'Face down card' }))[0]);

    await waitFor(() => expect(mocks.flip).toHaveBeenCalledTimes(1));
    expect(mocks.flip.mock.calls[0][0]).toBe('session-1');
    expect(await screen.findByRole('button', { name: '7♠' })).toBeInTheDocument();
  });

  it('ends the game only after a confirmation', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flippedCards: [] });
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
    mocks.getRingo.mockResolvedValue({ seed: 1, flippedCards: [] });
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

  it('sends guests back to the session page', async () => {
    mocks.getRingo.mockRejectedValue({ status: 403 });

    renderPage();

    expect(await screen.findByText('Session page')).toBeInTheDocument();
  });
});
