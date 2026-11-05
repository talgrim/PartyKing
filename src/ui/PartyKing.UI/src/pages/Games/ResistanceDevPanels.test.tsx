import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ResistanceDevPanels } from './ResistanceDevPanels';

const mocks = vi.hoisted(() => ({ getLobby: vi.fn(), join: vi.fn(), start: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getResistanceLobby: mocks.getLobby, joinResistance: mocks.join, startResistance: mocks.start },
}));

const renderPage = (search = '') =>
  render(
    <MemoryRouter initialEntries={[`/dev/s/session-1/resistance${search}`]}>
      <Routes>
        <Route path="/dev/s/:sessionId/resistance" element={<ResistanceDevPanels />} />
      </Routes>
    </MemoryRouter>,
  );

const lobby = { createdAt: '2026-10-29T10:00:00Z', isOpen: true, isStarted: false, joined: false };

describe('ResistanceDevPanels', () => {
  beforeEach(() => {
    mocks.getLobby.mockReset().mockResolvedValue(lobby);
    mocks.join.mockReset().mockResolvedValue(undefined);
    mocks.start.mockReset().mockResolvedValue(undefined);
  });

  it('joins one dev persona per player and shows a frame for each', async () => {
    renderPage('?n=6');

    await userEvent.click(screen.getByRole('button', { name: 'Join and show' }));

    await waitFor(() => expect(screen.getAllByTitle(/^Player \d$/)).toHaveLength(6));
    expect(mocks.join).toHaveBeenCalledTimes(6);
    expect(mocks.join).toHaveBeenCalledWith('session-1', 'Player 1', 'dev-1');
    expect(mocks.join).toHaveBeenCalledWith('session-1', 'Player 6', 'dev-6');
    expect(screen.getByTitle('Player 3')).toHaveAttribute('src', '/s/session-1/games/resistance/join?persona=dev-3');
    expect(screen.getByTitle('Player 3')).toHaveAttribute('name', 'partyking.devPersona:dev-3');
  });

  it('does not join a persona that already joined', async () => {
    mocks.getLobby.mockResolvedValue({ ...lobby, joined: true });
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Join and show' }));

    await waitFor(() => expect(screen.getAllByTitle(/^Player \d$/)).toHaveLength(5));
    expect(mocks.join).not.toHaveBeenCalled();
  });

  it('keeps the player count between 5 and 10', async () => {
    renderPage('?n=42');

    expect(screen.getByRole('spinbutton')).toHaveValue(10);
  });

  it('tells to unlock the session when joining is refused and shows no frames', async () => {
    mocks.getLobby.mockRejectedValue({ status: 401 });
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Join and show' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The session is locked in this browser');
    expect(screen.queryByTitle('Player 1')).not.toBeInTheDocument();
  });

  it('starts the game once the panels are shown', async () => {
    renderPage();
    const start = screen.getByRole('button', { name: 'Start game' });
    expect(start).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Join and show' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Start game' }));

    expect(mocks.start).toHaveBeenCalledWith('session-1');
  });
});
