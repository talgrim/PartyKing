import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { RingoEntry } from './RingoEntry';

const mocks = vi.hoisted(() => ({ getRingo: vi.fn(), startRingo: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getRingo: mocks.getRingo, startRingo: mocks.startRingo },
}));

const renderEntry = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/games']}>
        <Routes>
          <Route path="/s/:sessionId/games" element={<RingoEntry sessionId="session-1" />} />
          <Route path="/s/:sessionId/games/ringo" element={<p>Ringo board</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('RingoEntry', () => {
  beforeEach(() => {
    mocks.getRingo.mockReset();
    mocks.startRingo.mockReset();
  });

  it('offers to resume an existing game', async () => {
    mocks.getRingo.mockResolvedValue({ seed: 1, flippedCards: [] });

    renderEntry();

    expect(await screen.findByRole('link', { name: 'Resume' })).toHaveAttribute('href', '/s/session-1/games/ringo');
    expect(screen.queryByRole('button', { name: 'Start new' })).not.toBeInTheDocument();
  });

  it('starts a new game and opens the board when there is none', async () => {
    mocks.getRingo.mockRejectedValue({ status: 404 });
    mocks.startRingo.mockResolvedValue({ seed: 9, flippedCards: [] });
    renderEntry();

    await userEvent.click(await screen.findByRole('button', { name: 'Start new' }));

    await waitFor(() => expect(mocks.startRingo).toHaveBeenCalledWith('session-1'));
    expect(await screen.findByText('Ringo board')).toBeInTheDocument();
  });

  it('shows a message when the game can not be started', async () => {
    mocks.getRingo.mockRejectedValue({ status: 404 });
    mocks.startRingo.mockRejectedValue({ status: 409 });
    renderEntry();

    await userEvent.click(await screen.findByRole('button', { name: 'Start new' }));

    expect(await screen.findByText('Could not start the game.')).toBeInTheDocument();
  });
});
