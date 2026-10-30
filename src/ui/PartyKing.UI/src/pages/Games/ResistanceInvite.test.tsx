import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { ResistanceInvite } from './ResistanceInvite';

const mocks = vi.hoisted(() => ({ getLobby: vi.fn() }));

vi.mock('@/service/endpoints/games/gamesApi', () => ({ gamesApi: { getResistanceLobby: mocks.getLobby } }));

const open = { createdAt: '2026-10-29T10:00:00Z', isOpen: true, isStarted: false, joined: false };

const renderInvite = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <ResistanceInvite sessionId="session-1" enabled />
    </SWRConfig>,
  );

describe('ResistanceInvite', () => {
  beforeEach(() => {
    mocks.getLobby.mockReset();
    sessionStorage.clear();
  });

  it('invites a guest while the game is open and dismisses on "Nie"', async () => {
    mocks.getLobby.mockResolvedValue(open);
    renderInvite();

    expect(await screen.findByText('Nowa gra w Resistance została uruchomiona. Czy chcesz dołączyć?')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Nie' }));

    expect(screen.queryByText(/Nowa gra w Resistance/)).not.toBeInTheDocument();
  });

  it('opens the join page in a new tab on "Tak"', async () => {
    const spy = vi.spyOn(window, 'open').mockReturnValue(null);
    mocks.getLobby.mockResolvedValue(open);
    renderInvite();

    await userEvent.click(await screen.findByRole('button', { name: 'Tak' }));

    expect(spy).toHaveBeenCalledWith('/s/session-1/games/resistance/join', '_blank', 'noopener,noreferrer');
    spy.mockRestore();
  });

  it.each([
    ['the game is not open', { ...open, isOpen: false }],
    ['the guest already joined', { ...open, joined: true }],
    ['the game started', { ...open, isStarted: true }],
  ])('shows nothing when %s', async (_, lobby) => {
    mocks.getLobby.mockResolvedValue(lobby);
    renderInvite();

    await vi.waitFor(() => expect(mocks.getLobby).toHaveBeenCalled());
    expect(screen.queryByText(/Nowa gra w Resistance/)).not.toBeInTheDocument();
  });
});
