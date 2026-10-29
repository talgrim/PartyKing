import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { RingoJoin } from './RingoJoin';

const mocks = vi.hoisted(() => ({ getLobby: vi.fn(), join: vi.fn() }));

vi.mock('@/service/realtime/useSessionMusicHub', () => ({ useSessionMusicHub: vi.fn() }));
vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({
  useViewerRole: () => ({ isHost: false, isAdmin: false }),
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
});
