import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { RingoJoin } from './RingoJoin';

const mocks = vi.hoisted(() => ({ getLobby: vi.fn(), join: vi.fn(), role: { isHost: false, isAdmin: false } }));

vi.mock('@/service/realtime/useSessionMusicHub', () => ({ useSessionMusicHub: vi.fn() }));
vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({
  useViewerRole: () => mocks.role,
}));
vi.mock('./Ringo', () => ({ Ringo: () => <p>Board</p> }));
vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getRingoLobby: mocks.getLobby, joinRingo: mocks.join },
}));

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/games/ringo/join']}>
        <Routes>
          <Route path="/s/:sessionId/games/ringo/join" element={<RingoJoin />} />
          <Route path="/s/:sessionId" element={<p>Session page</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('RingoJoin', () => {
  beforeEach(() => {
    mocks.getLobby.mockReset();
    mocks.join.mockReset();
    mocks.role = { isHost: false, isAdmin: false };
  });

  it('asks for a name and shows the board once the guest joined', async () => {
    mocks.getLobby
      .mockResolvedValueOnce({ seed: 1, isOpen: true, joined: false })
      .mockResolvedValue({ seed: 1, isOpen: true, joined: true });
    mocks.join.mockResolvedValue(undefined);
    renderPage();

    await userEvent.type(await screen.findByLabelText(/Twoje imię/), ' Ola ');
    await userEvent.click(screen.getByRole('button', { name: 'Dołącz' }));

    expect(mocks.join).toHaveBeenCalledWith('session-1', 'Ola');
    expect(await screen.findByText('Board')).toBeInTheDocument();
  });

  it('says when the name is taken', async () => {
    mocks.getLobby.mockResolvedValue({ seed: 1, isOpen: true, joined: false });
    mocks.join.mockRejectedValue({ status: 409 });
    renderPage();

    await userEvent.type(await screen.findByLabelText(/Twoje imię/), 'Ola');
    await userEvent.click(screen.getByRole('button', { name: 'Dołącz' }));

    expect(await screen.findByText(/Ktoś już używa tego imienia/)).toBeInTheDocument();
  });

  it('says the game is not open yet', async () => {
    mocks.getLobby.mockResolvedValue({ seed: 1, isOpen: false, joined: false });
    renderPage();

    expect(await screen.findByText(/nie jest jeszcze otwarta/)).toBeInTheDocument();
  });

  it('goes back to the session when there is no game', async () => {
    mocks.getLobby.mockRejectedValue({ status: 404 });
    renderPage();

    await waitFor(() => expect(screen.getByText('Session page')).toBeInTheDocument());
  });

  it('lets an admin join without a name, which the server turns into "admin"', async () => {
    mocks.role = { isHost: false, isAdmin: true };
    mocks.getLobby.mockResolvedValue({ seed: 1, isOpen: true, joined: false });
    mocks.join.mockResolvedValue(undefined);
    renderPage();

    expect(await screen.findByText('Bez imienia dołączysz jako „admin”.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Dołącz' }));

    expect(mocks.join).toHaveBeenCalledWith('session-1', '');
  });

  it('lets an admin join under a typed name', async () => {
    mocks.role = { isHost: false, isAdmin: true };
    mocks.getLobby.mockResolvedValue({ seed: 1, isOpen: true, joined: false });
    mocks.join.mockResolvedValue(undefined);
    renderPage();

    await userEvent.type(await screen.findByLabelText(/Twoje imię/), 'Adam');
    await userEvent.click(screen.getByRole('button', { name: 'Dołącz' }));

    expect(mocks.join).toHaveBeenCalledWith('session-1', 'Adam');
  });

  it('asks the host for no name', async () => {
    mocks.role = { isHost: true, isAdmin: false };
    mocks.getLobby.mockResolvedValue({ seed: 1, isOpen: true, joined: false });
    renderPage();

    await screen.findByRole('button', { name: 'Dołącz' });
    expect(screen.queryByLabelText(/Twoje imię/)).not.toBeInTheDocument();
  });
});
