import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ResistanceEntry } from './ResistanceEntry';

const mocks = vi.hoisted(() => ({ getResistance: vi.fn(), createResistance: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getResistance: mocks.getResistance, createResistance: mocks.createResistance },
}));

const lobby = { createdAt: '2026-10-29T10:00:00Z', isOpen: false, players: [], yourPlayerId: null, maxPlayers: 10 };

const renderEntry = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/games']}>
        <Routes>
          <Route path="/s/:sessionId/games" element={<ResistanceEntry sessionId="session-1" />} />
          <Route path="/s/:sessionId/games/resistance" element={<p>Resistance board</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('ResistanceEntry', () => {
  beforeEach(() => {
    mocks.getResistance.mockReset();
    mocks.createResistance.mockReset();
  });

  it('offers to resume an existing lobby', async () => {
    mocks.getResistance.mockResolvedValue(lobby);
    renderEntry();

    expect(await screen.findByRole('link', { name: 'Resume' })).toHaveAttribute('href', '/s/session-1/games/resistance');
    expect(screen.queryByRole('button', { name: 'Create' })).not.toBeInTheDocument();
  });

  it('creates the lobby and opens the board when there is none', async () => {
    mocks.getResistance.mockRejectedValue({ status: 404 });
    mocks.createResistance.mockResolvedValue(lobby);
    renderEntry();

    await userEvent.click(await screen.findByRole('button', { name: 'Create' }));

    await waitFor(() => expect(mocks.createResistance).toHaveBeenCalledWith('session-1'));
    expect(await screen.findByText('Resistance board')).toBeInTheDocument();
  });

  it('says so when the lobby could not be created', async () => {
    mocks.getResistance.mockRejectedValue({ status: 404 });
    mocks.createResistance.mockRejectedValue({ status: 500 });
    renderEntry();

    await userEvent.click(await screen.findByRole('button', { name: 'Create' }));

    expect(await screen.findByText('Could not create the game.')).toBeInTheDocument();
  });
});
