import { render, screen } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { Games } from './Games';

const mocks = vi.hoisted(() => ({ getGames: vi.fn(), getSuggestions: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getGames: mocks.getGames, getNeverHaveIEverSuggestions: mocks.getSuggestions },
}));

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/games']}>
        <Routes>
          <Route path="/s/:sessionId/games" element={<Games />} />
          <Route path="/s/:sessionId" element={<p>Session page</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('Games', () => {
  beforeEach(() => {
    mocks.getGames.mockReset();
  });

  it('lists the games of the session', async () => {
    mocks.getGames.mockResolvedValue([
      { id: 'ringo', name: 'Ringo', description: 'A party game.' },
      { id: 'never-have-i-ever', name: 'Never have I ever', description: 'Confess.' },
    ]);

    renderPage();

    expect(await screen.findByText('Ringo')).toBeInTheDocument();
    expect(screen.getByText('Never have I ever')).toBeInTheDocument();
    expect(mocks.getGames).toHaveBeenCalledWith('session-1');
    expect(screen.getByRole('link', { name: 'Back to the session' })).toHaveAttribute('href', '/s/session-1');
  });

  it('links to the Resistance board', async () => {
    mocks.getGames.mockResolvedValue([{ id: 'resistance', name: 'Resistance', description: 'Gra dedukcji społecznej.' }]);
    renderPage();

    expect(await screen.findByRole('link', { name: 'Open' })).toHaveAttribute('href', '/s/session-1/games/resistance');
  });

  it('offers suggestions only for Never have I ever', async () => {
    mocks.getGames.mockResolvedValue([
      { id: 'ringo', name: 'Ringo', description: 'A party game.' },
      { id: 'never-have-i-ever', name: 'Never have I ever', description: 'Confess.' },
    ]);

    renderPage();

    expect(await screen.findAllByRole('button', { name: 'Suggest statements' })).toHaveLength(1);
  });

  it('says so when there are no games', async () => {
    mocks.getGames.mockResolvedValue([]);

    renderPage();

    expect(await screen.findByText('There are no games yet.')).toBeInTheDocument();
  });

  it('sends a guest back to the session page', async () => {
    mocks.getGames.mockRejectedValue({ status: 403, message: 'Forbidden' });

    renderPage();

    expect(await screen.findByText('Session page')).toBeInTheDocument();
  });

  it('asks for the session password when the session is locked', async () => {
    mocks.getGames.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    renderPage();

    expect(await screen.findByLabelText('Session password')).toBeInTheDocument();
  });

  it('says when the session does not exist', async () => {
    mocks.getGames.mockRejectedValue({ status: 404, message: 'Not found' });

    renderPage();

    expect(await screen.findByText(/This session does not exist/)).toBeInTheDocument();
  });

  it('says when the games could not be loaded', async () => {
    mocks.getGames.mockRejectedValue({ status: 500, message: 'Boom' });

    renderPage();

    expect(await screen.findByText(/Could not load the games/)).toBeInTheDocument();
  });
});
