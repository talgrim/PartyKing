import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { OpenGamesMenu } from './OpenGamesMenu';

const mocks = vi.hoisted(() => ({ getRingoLobby: vi.fn(), getResistanceLobby: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({
  gamesApi: { getRingoLobby: mocks.getRingoLobby, getResistanceLobby: mocks.getResistanceLobby },
}));

const lobby = (isOpen: boolean) => ({ seed: 1, createdAt: '2026-10-29T10:00:00Z', isOpen, joined: false });

const renderMenu = (canManage: boolean, refreshInterval = 0) =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0, refreshInterval }}>
      <OpenGamesMenu sessionId="session-1" canManage={canManage} />
    </SWRConfig>,
  );

describe('OpenGamesMenu', () => {
  beforeEach(() => {
    mocks.getRingoLobby.mockReset();
    mocks.getResistanceLobby.mockReset();
  });

  it('is hidden while no game is open', async () => {
    mocks.getRingoLobby.mockResolvedValue(lobby(false));
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    renderMenu(false);

    await vi.waitFor(() => expect(mocks.getRingoLobby).toHaveBeenCalled());
    expect(screen.queryByRole('button', { name: 'Open games' })).not.toBeInTheDocument();
  });

  it('lists the open games and opens a guest\'s choice on its join page in a new tab', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockResolvedValue(lobby(false));
    mocks.getResistanceLobby.mockResolvedValue(lobby(true));
    renderMenu(false);

    await userEvent.click(await screen.findByRole('button', { name: 'Open games' }));
    expect(screen.queryByRole('menuitem', { name: 'Ringo' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Resistance' }));

    expect(open).toHaveBeenCalledWith('/s/session-1/games/resistance/join', '_blank', 'noopener,noreferrer');
    open.mockRestore();
  });

  it('sends the host and admins straight to the board', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getRingoLobby.mockResolvedValue(lobby(true));
    mocks.getResistanceLobby.mockResolvedValue(lobby(true));
    renderMenu(true);

    await userEvent.click(await screen.findByRole('button', { name: 'Open games' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Ringo' }));

    expect(open).toHaveBeenCalledWith('/s/session-1/games/ringo', '_blank', 'noopener,noreferrer');
    open.mockRestore();
  });

  it('disappears once the game was ended', async () => {
    mocks.getRingoLobby.mockResolvedValueOnce(lobby(true)).mockRejectedValue({ status: 404 });
    mocks.getResistanceLobby.mockRejectedValue({ status: 404 });
    renderMenu(false, 50);

    await screen.findByRole('button', { name: 'Open games' });

    await vi.waitFor(() => expect(screen.queryByRole('button', { name: 'Open games' })).not.toBeInTheDocument());
  });
});
