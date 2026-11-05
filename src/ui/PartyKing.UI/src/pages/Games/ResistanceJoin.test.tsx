import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ResistanceJoin } from './ResistanceJoin';

const mocks = vi.hoisted(() => ({ getLobby: vi.fn(), join: vi.fn(), getSeats: vi.fn(), claim: vi.fn(), viewer: { isHost: false, isAdmin: false } }));

vi.mock('@/service/realtime/useSessionMusicHub', () => ({ useSessionMusicHub: vi.fn() }));
vi.mock('@/service/endpoints/sessions/useViewerRole', () => ({
  useViewerRole: () => mocks.viewer,
}));
vi.mock('./Resistance', () => ({ Resistance: () => <p>Board</p> }));
vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: {
    getResistanceLobby: mocks.getLobby,
    joinResistance: mocks.join,
    getResistanceClaimableSeats: mocks.getSeats,
    claimResistanceSeat: mocks.claim,
  },
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
    mocks.getSeats.mockReset();
    mocks.claim.mockReset();
    mocks.viewer.isHost = false;
    mocks.viewer.isAdmin = false;
  });

  it('asks for a name and shows the board once the guest joined', async () => {
    mocks.getLobby.mockResolvedValueOnce(open).mockResolvedValue({ ...open, joined: true });
    mocks.join.mockResolvedValue(undefined);
    renderPage();

    await userEvent.type(await screen.findByLabelText(/Your name/), ' Ola ');
    await userEvent.click(screen.getByRole('button', { name: 'Join' }));

    expect(mocks.join).toHaveBeenCalledWith('session-1', 'Ola');
    expect(await screen.findByText('Board')).toBeInTheDocument();
  });

  it('says when the game is full', async () => {
    mocks.getLobby.mockResolvedValue(open);
    mocks.join.mockRejectedValue({ status: 409 });
    renderPage();

    await userEvent.type(await screen.findByLabelText(/Your name/), 'Ola');
    await userEvent.click(screen.getByRole('button', { name: 'Join' }));

    expect(await screen.findByText(/full/)).toBeInTheDocument();
  });

  it('says the game is not open yet', async () => {
    mocks.getLobby.mockResolvedValue({ ...open, isOpen: false });
    renderPage();

    expect(await screen.findByText(/not open yet/)).toBeInTheDocument();
  });

  it('says the game already started instead of showing the form', async () => {
    mocks.getLobby.mockResolvedValue({ ...open, isStarted: true });
    renderPage();

    expect(await screen.findByText(/has already started/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Your name/)).not.toBeInTheDocument();
  });

  it('goes back to the session when there is no game', async () => {
    mocks.getLobby.mockRejectedValue({ status: 404 });
    renderPage();

    await waitFor(() => expect(screen.getByText('Session page')).toBeInTheDocument());
  });
});

describe('ResistanceJoin, returning to a started game', () => {
  const started = { ...open, isStarted: true };

  beforeEach(() => {
    mocks.getLobby.mockReset();
    mocks.join.mockReset();
    mocks.getSeats.mockReset();
    mocks.claim.mockReset();
    mocks.viewer.isHost = false;
    mocks.viewer.isAdmin = false;
  });

  it('lists the seats the host released, with no name field', async () => {
    mocks.getLobby.mockResolvedValue(started);
    mocks.getSeats.mockResolvedValue([
      { id: 'p1', name: 'Ola' },
      { id: 'p2', name: 'Bartek' },
    ]);
    renderPage();

    expect(await screen.findByRole('button', { name: 'Take over: Ola' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Take over: Bartek' })).toBeInTheDocument();
    expect(screen.queryByLabelText(/Your name/)).not.toBeInTheDocument();
  });

  it('takes the chosen seat over without a name and shows the board', async () => {
    mocks.getLobby.mockResolvedValueOnce(started).mockResolvedValue({ ...started, joined: true });
    mocks.getSeats.mockResolvedValue([{ id: 'p1', name: 'Ola' }]);
    mocks.claim.mockResolvedValue(undefined);
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Take over: Ola' }));

    expect(mocks.claim).toHaveBeenCalledWith('session-1', 'p1');
    expect(mocks.join).not.toHaveBeenCalled();
    expect(await screen.findByText('Board')).toBeInTheDocument();
  });

  it('says when no seat is free', async () => {
    mocks.getLobby.mockResolvedValue(started);
    mocks.getSeats.mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText(/No seat is free to take over/)).toBeInTheDocument();
  });

  it('tells the player when the seat is gone and fetches the seats again', async () => {
    mocks.getLobby.mockResolvedValue(started);
    mocks.getSeats.mockResolvedValue([{ id: 'p1', name: 'Ola' }]);
    mocks.claim.mockRejectedValue({ status: 409 });
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Take over: Ola' }));

    expect(await screen.findByText(/Could not take over that seat/)).toBeInTheDocument();
    await waitFor(() => expect(mocks.getSeats.mock.calls.length).toBeGreaterThan(1));
  });

  it('offers the session host their own seat back', async () => {
    mocks.viewer.isHost = true;
    mocks.getLobby.mockResolvedValueOnce(started).mockResolvedValue({ ...started, joined: true });
    mocks.getSeats.mockResolvedValue([]);
    mocks.join.mockResolvedValue(undefined);
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Rejoin as the host' }));

    expect(mocks.join).toHaveBeenCalledWith('session-1', '');
    expect(await screen.findByText('Board')).toBeInTheDocument();
  });

  it('does not offer the host button to a guest', async () => {
    mocks.getLobby.mockResolvedValue(started);
    mocks.getSeats.mockResolvedValue([]);
    renderPage();

    await screen.findByText(/has already started/);
    expect(screen.queryByRole('button', { name: 'Rejoin as the host' })).not.toBeInTheDocument();
  });

  it('does not ask for the seats while the game has not started', async () => {
    mocks.getLobby.mockResolvedValue(open);
    renderPage();

    await screen.findByLabelText(/Your name/);
    expect(mocks.getSeats).not.toHaveBeenCalled();
  });
});
