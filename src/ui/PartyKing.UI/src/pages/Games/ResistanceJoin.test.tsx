import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ResistanceJoin } from './ResistanceJoin';

const mocks = vi.hoisted(() => ({ getLobby: vi.fn(), join: vi.fn() }));

vi.mock('@/service/realtime/useSessionMusicHub', () => ({ useSessionMusicHub: vi.fn() }));
vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({
  useViewerRole: () => ({ isHost: false, isAdmin: false }),
}));
vi.mock('./Resistance', () => ({ Resistance: () => <p>Board</p> }));
vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getResistanceLobby: mocks.getLobby, joinResistance: mocks.join },
}));

const open = { createdAt: '2026-10-29T10:00:00Z', isOpen: true, isStarted: false, joined: false };

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1/games/resistance/join']}>
        <Routes>
          <Route path="/s/:sessionId/games/resistance/join" element={<ResistanceJoin />} />
          <Route path="/s/:sessionId" element={<p>Session page</p>} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('ResistanceJoin', () => {
  beforeEach(() => {
    mocks.getLobby.mockReset();
    mocks.join.mockReset();
  });

  it('asks for a name and shows the board once the guest joined', async () => {
    mocks.getLobby.mockResolvedValueOnce(open).mockResolvedValue({ ...open, joined: true });
    mocks.join.mockResolvedValue(undefined);
    renderPage();

    await userEvent.type(await screen.findByLabelText(/Twoje imię/), ' Ola ');
    await userEvent.click(screen.getByRole('button', { name: 'Dołącz' }));

    expect(mocks.join).toHaveBeenCalledWith('session-1', 'Ola');
    expect(await screen.findByText('Board')).toBeInTheDocument();
  });

  it('says when the game is full', async () => {
    mocks.getLobby.mockResolvedValue(open);
    mocks.join.mockRejectedValue({ status: 409 });
    renderPage();

    await userEvent.type(await screen.findByLabelText(/Twoje imię/), 'Ola');
    await userEvent.click(screen.getByRole('button', { name: 'Dołącz' }));

    expect(await screen.findByText(/pełna/)).toBeInTheDocument();
  });

  it('says the game is not open yet', async () => {
    mocks.getLobby.mockResolvedValue({ ...open, isOpen: false });
    renderPage();

    expect(await screen.findByText(/nie jest jeszcze otwarta/)).toBeInTheDocument();
  });

  it('says the game already started instead of showing the form', async () => {
    mocks.getLobby.mockResolvedValue({ ...open, isStarted: true });
    renderPage();

    expect(await screen.findByText(/już się rozpoczęła/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Twoje imię/)).not.toBeInTheDocument();
  });

  it('goes back to the session when there is no game', async () => {
    mocks.getLobby.mockRejectedValue({ status: 404 });
    renderPage();

    await waitFor(() => expect(screen.getByText('Session page')).toBeInTheDocument());
  });
});
