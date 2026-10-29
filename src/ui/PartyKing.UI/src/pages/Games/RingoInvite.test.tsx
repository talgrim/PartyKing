import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { RingoInvite } from './RingoInvite';

const mocks = vi.hoisted(() => ({ getLobby: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({ gamesApi: { getRingoLobby: mocks.getLobby } }));

const renderInvite = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <RingoInvite sessionId="session-1" enabled />
    </SWRConfig>,
  );

describe('RingoInvite', () => {
  beforeEach(() => {
    mocks.getLobby.mockReset();
    sessionStorage.clear();
  });

  it('invites a guest while the game is open and dismisses on "Nie"', async () => {
    mocks.getLobby.mockResolvedValue({ seed: 1, isOpen: true, joined: false });
    renderInvite();

    expect(await screen.findByText('Nowa gra w ringo została uruchomiona. Czy chcesz dołączyć?')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Nie' }));

    expect(screen.queryByText(/Nowa gra w ringo/)).not.toBeInTheDocument();
  });

  it('opens the join page in a new tab on "Tak"', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getLobby.mockResolvedValue({ seed: 1, isOpen: true, joined: false });
    renderInvite();

    await userEvent.click(await screen.findByRole('button', { name: 'Tak' }));

    expect(open).toHaveBeenCalledWith('/s/session-1/games/ringo/join', '_blank', 'noopener,noreferrer');
    expect(screen.queryByText(/Nowa gra w ringo/)).not.toBeInTheDocument();
    open.mockRestore();
  });

  it.each([
    ['the game is not open', { seed: 1, isOpen: false, joined: false }],
    ['the guest already joined', { seed: 1, isOpen: true, joined: true }],
  ])('shows nothing when %s', async (_, lobby) => {
    mocks.getLobby.mockResolvedValue(lobby);
    renderInvite();

    await vi.waitFor(() => expect(mocks.getLobby).toHaveBeenCalled());
    expect(screen.queryByText(/Nowa gra w ringo/)).not.toBeInTheDocument();
  });
});
