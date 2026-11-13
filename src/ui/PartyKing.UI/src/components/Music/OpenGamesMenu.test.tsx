import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { OpenGamesMenu } from './OpenGamesMenu';

const mocks = vi.hoisted(() => ({
  getRingoLobby: vi.fn(),
  getResistanceLobby: vi.fn(),
  startRingo: vi.fn(),
  createResistance: vi.fn(),
}));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: {
    getRingoLobby: mocks.getRingoLobby,
    getResistanceLobby: mocks.getResistanceLobby,
    startRingo: mocks.startRingo,
    createResistance: mocks.createResistance,
  },
}));

const lobby = (isOpen: boolean, extra: object = {}) => ({
  seed: 1,
  createdAt: '2026-10-29T10:00:00Z',
  isOpen,
  isStarted: false,
  joined: false,
  ...extra,
});

const renderMenu = (canManage: boolean, refreshInterval = 0) =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0, refreshInterval }}>
      <OpenGamesMenu sessionId="session-1" canManage={canManage} />
    </SWRConfig>,
  );

const gamepad = () => screen.getByRole('button', { name: 'Open games' });

describe('OpenGamesMenu', () => {
  beforeEach(() => {
    mocks.getRingoLobby.mockReset();
    mocks.getResistanceLobby.mockReset();
    mocks.startRingo.mockReset();
    mocks.createResistance.mockReset();
  });

  it('is shown, but disabled, to a guest while no game is open', async () => {
    mocks.getRingoLobby.mockResolvedValue(lobby(false));
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    renderMenu(false);

    await vi.waitFor(() => expect(mocks.getRingoLobby).toHaveBeenCalled());
    expect(gamepad()).toBeDisabled();
  });

  it('enables a guest\'s icon once the host opens a game', async () => {
    mocks.getRingoLobby.mockResolvedValueOnce(lobby(false)).mockResolvedValue(lobby(true));
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    renderMenu(false, 50);

    await vi.waitFor(() => expect(gamepad()).toBeEnabled());
  });

  it('lists all the games to a guest, but only an open one can be chosen, on its join page in a new tab', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockResolvedValue(lobby(false));
    mocks.getResistanceLobby.mockResolvedValue(lobby(true));
    renderMenu(false);

    await vi.waitFor(() => expect(gamepad()).toBeEnabled());
    await userEvent.click(gamepad());
    expect(screen.getByRole('menuitem', { name: 'Ringo' })).toHaveAttribute('aria-disabled', 'true');
    expect(screen.queryByRole('menuitem', { name: 'Never have I ever' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Resistance' }));

    expect(open).toHaveBeenCalledWith('/s/session-1/games/resistance/join', '_blank', 'noopener,noreferrer');
    open.mockRestore();
  });

  it('is always enabled for the host, which lists every game and marks those with a lobby', async () => {
    mocks.getRingoLobby.mockResolvedValue(lobby(false));
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    renderMenu(true);

    await vi.waitFor(() => expect(mocks.getRingoLobby).toHaveBeenCalled());
    expect(gamepad()).toBeEnabled();
    await userEvent.click(gamepad());

    expect(screen.getByRole('menuitem', { name: 'Ringo Lobby' })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'Resistance' })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'Never have I ever' })).toBeInTheDocument();
  });

  it('marks a game that is open to guests', async () => {
    mocks.getRingoLobby.mockResolvedValue(lobby(true));
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    renderMenu(true);

    await userEvent.click(gamepad());

    expect(await screen.findByRole('menuitem', { name: 'Ringo Open' })).toBeInTheDocument();
  });

  it('sends the host and admins straight to the board', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockResolvedValue(lobby(true));
    mocks.getResistanceLobby.mockResolvedValue(lobby(true));
    renderMenu(true);

    await userEvent.click(gamepad());
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Ringo Open' }));

    expect(open).toHaveBeenCalledWith('/s/session-1/games/ringo', '_blank', 'noopener,noreferrer');
    expect(mocks.startRingo).not.toHaveBeenCalled();
    open.mockRestore();
  });

  it('opens the TV board and the player page for the host and admins in an open Resistance game', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockResolvedValue(lobby(false));
    mocks.getResistanceLobby.mockResolvedValue(lobby(true));
    renderMenu(true);

    await userEvent.click(gamepad());
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Resistance Open' }));

    expect(open).toHaveBeenNthCalledWith(1, '/s/session-1/games/resistance?tv=1', '_blank', 'noopener,noreferrer');
    expect(open).toHaveBeenNthCalledWith(2, '/s/session-1/games/resistance/join', '_blank', 'noopener,noreferrer');
    open.mockRestore();
  });

  it('opens only the TV board for a Resistance lobby the host has not opened yet', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockRejectedValue({ status: 404 });
    mocks.getResistanceLobby.mockResolvedValue(lobby(false));
    renderMenu(true);

    await userEvent.click(gamepad());
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Resistance Lobby' }));

    expect(open).toHaveBeenCalledTimes(1);
    expect(open).toHaveBeenCalledWith('/s/session-1/games/resistance?tv=1', '_blank', 'noopener,noreferrer');
    open.mockRestore();
  });

  it('opens only the TV board once Resistance started without the host', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockResolvedValue(lobby(false));
    mocks.getResistanceLobby.mockResolvedValue(lobby(true, { isStarted: true }));
    renderMenu(true);

    await userEvent.click(gamepad());
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Resistance Open' }));

    expect(open).toHaveBeenCalledTimes(1);
    expect(open).toHaveBeenCalledWith('/s/session-1/games/resistance?tv=1', '_blank', 'noopener,noreferrer');
    open.mockRestore();
  });

  it('creates the Ringo lobby of a host\'s click and opens its board in a new tab', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockRejectedValue({ status: 404 });
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    mocks.startRingo.mockResolvedValue({});
    renderMenu(true);

    await vi.waitFor(() => expect(mocks.getRingoLobby).toHaveBeenCalled());
    await userEvent.click(gamepad());
    await userEvent.click(screen.getByRole('menuitem', { name: 'Ringo' }));

    expect(mocks.startRingo).toHaveBeenCalledWith('session-1');
    await vi.waitFor(() =>
      expect(open).toHaveBeenCalledWith('/s/session-1/games/ringo', '_blank', 'noopener,noreferrer'),
    );
    open.mockRestore();
  });

  it('creates the Resistance lobby of a host\'s click and opens its TV board in a new tab', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockRejectedValue({ status: 404 });
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    mocks.createResistance.mockResolvedValue({});
    renderMenu(true);

    await vi.waitFor(() => expect(mocks.getResistanceLobby).toHaveBeenCalled());
    await userEvent.click(gamepad());
    await userEvent.click(screen.getByRole('menuitem', { name: 'Resistance' }));

    expect(mocks.createResistance).toHaveBeenCalledWith('session-1');
    await vi.waitFor(() =>
      expect(open).toHaveBeenCalledWith('/s/session-1/games/resistance?tv=1', '_blank', 'noopener,noreferrer'),
    );
    expect(open).toHaveBeenCalledTimes(1);
    open.mockRestore();
  });

  it('opens a lobby that was created meanwhile instead of failing', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockRejectedValue({ status: 404 });
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    mocks.startRingo.mockRejectedValue({ status: 409 });
    renderMenu(true);

    await vi.waitFor(() => expect(mocks.getRingoLobby).toHaveBeenCalled());
    await userEvent.click(gamepad());
    await userEvent.click(screen.getByRole('menuitem', { name: 'Ringo' }));

    await vi.waitFor(() =>
      expect(open).toHaveBeenCalledWith('/s/session-1/games/ringo', '_blank', 'noopener,noreferrer'),
    );
    open.mockRestore();
  });

  it('opens nothing when a lobby cannot be created', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockRejectedValue({ status: 404 });
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    mocks.startRingo.mockRejectedValue({ status: 500 });
    renderMenu(true);

    await vi.waitFor(() => expect(mocks.getRingoLobby).toHaveBeenCalled());
    await userEvent.click(gamepad());
    await userEvent.click(screen.getByRole('menuitem', { name: 'Ringo' }));

    await vi.waitFor(() => expect(mocks.startRingo).toHaveBeenCalled());
    expect(open).not.toHaveBeenCalled();
    open.mockRestore();
  });

  it('sends the host to the games page for Never have I ever', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockRejectedValue({ status: 404 });
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    renderMenu(true);

    await userEvent.click(gamepad());
    await userEvent.click(screen.getByRole('menuitem', { name: 'Never have I ever' }));

    expect(open).toHaveBeenCalledWith('/s/session-1/games', '_blank', 'noopener,noreferrer');
    open.mockRestore();
  });

  it('disables a guest\'s icon again once the open game was ended', async () => {
    mocks.getRingoLobby
      .mockResolvedValueOnce(lobby(true))
      .mockImplementation(() => new Promise((_, reject) => setTimeout(() => reject({ status: 404 }), 300)));
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    renderMenu(false, 50);

    await vi.waitFor(() => expect(gamepad()).toBeEnabled());

    await vi.waitFor(() => expect(gamepad()).toBeDisabled());
  });
});
