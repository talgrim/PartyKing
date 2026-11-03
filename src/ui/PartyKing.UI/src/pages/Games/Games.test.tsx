import { render, screen } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { setLanguage } from '@/i18n';
import { Games } from './Games';

const mocks = vi.hoisted(() => ({ getGames: vi.fn(), getSuggestions: vi.fn(), getResistance: vi.fn(), getRingo: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: {
    getGames: mocks.getGames,
    getNeverHaveIEverSuggestions: mocks.getSuggestions,
    getResistance: mocks.getResistance,
    getRingo: mocks.getRingo,
  },
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
    mocks.getResistance.mockReset();
    mocks.getRingo.mockReset();
    mocks.getResistance.mockRejectedValue({ status: 404 });
    mocks.getRingo.mockRejectedValue({ status: 404 });
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

  it('offers to create the Resistance lobby, and to resume it once it exists', async () => {
    mocks.getGames.mockResolvedValue([{ id: 'resistance', name: 'Resistance', description: 'Gra dedukcji społecznej.' }]);
    mocks.getResistance.mockRejectedValueOnce({ status: 404 });
    renderPage();

    expect(await screen.findByRole('button', { name: 'Create' })).toBeInTheDocument();
  });

  it('labels the page in Polish', async () => {
    await setLanguage('pl');
    mocks.getGames.mockResolvedValue([{ id: 'resistance', name: 'Resistance', description: 'Gra dedukcji społecznej.' }]);
    mocks.getResistance.mockResolvedValue({ isOpen: false, players: [], yourPlayerId: null, maxPlayers: 10 });
    renderPage();

    expect(await screen.findByRole('link', { name: 'Wznów' })).toHaveAttribute('href', '/s/session-1/games/resistance');
    expect(screen.getByRole('link', { name: 'Wróć do sesji' })).toHaveAttribute('href', '/s/session-1');
  });

  it('links to the Resistance board when the lobby exists', async () => {
    mocks.getGames.mockResolvedValue([{ id: 'resistance', name: 'Resistance', description: 'Gra dedukcji społecznej.' }]);
    mocks.getResistance.mockResolvedValue({ isOpen: false, players: [], yourPlayerId: null, maxPlayers: 10 });
    renderPage();

    expect(await screen.findByRole('link', { name: 'Resume' })).toHaveAttribute('href', '/s/session-1/games/resistance');
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
