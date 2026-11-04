import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ResistancePlayerDto } from '@/service/endpoints/games/types';
import { ResistanceSeatsDialog } from './ResistanceSeatsDialog';

const mocks = vi.hoisted(() => ({ presence: vi.fn() }));

vi.mock('@/service/endpoints/games/useResistancePresence', () => ({ useResistancePresence: mocks.presence }));

const player = (id: string, name: string, extra: Partial<ResistancePlayerDto> = {}): ResistancePlayerDto => ({
  id,
  name,
  isSeatReleased: false,
  isManagerSeat: false,
  ...extra,
});

const renderDialog = (players: ResistancePlayerDto[], onSetReleased = vi.fn().mockResolvedValue(undefined)) => {
  render(<ResistanceSeatsDialog sessionId="s1" open onClose={vi.fn()} players={players} onSetReleased={onSetReleased} />);

  return onSetReleased;
};

describe('ResistanceSeatsDialog', () => {
  beforeEach(() => {
    mocks.presence.mockReset();
    mocks.presence.mockReturnValue({ onlinePlayerIds: ['b'] });
  });

  it('lists the players with their connection status, without the seats of the host and admins', () => {
    renderDialog([player('a', 'Ola'), player('b', 'Bartek'), player('h', 'Hanna (host)', { isManagerSeat: true })]);

    expect(screen.getByText('Ola')).toBeInTheDocument();
    expect(screen.getByText('Offline')).toBeInTheDocument();
    expect(screen.getByText('Online')).toBeInTheDocument();
    expect(screen.queryByText('Hanna (host)')).not.toBeInTheDocument();
  });

  it('releases the seat of an offline player', async () => {
    const onSetReleased = renderDialog([player('a', 'Ola')]);

    await userEvent.click(screen.getByRole('button', { name: 'Release the seat of Ola' }));

    expect(onSetReleased).toHaveBeenCalledWith(expect.objectContaining({ id: 'a' }), true);
  });

  it('does not let an online player be released', () => {
    renderDialog([player('b', 'Bartek')]);

    expect(screen.getByRole('button', { name: 'Release the seat of Bartek' })).toBeDisabled();
  });

  it('takes a release back, even for a player who is online again', async () => {
    const onSetReleased = renderDialog([player('b', 'Bartek', { isSeatReleased: true })]);

    await userEvent.click(screen.getByRole('button', { name: 'Take back the release of Bartek' }));

    expect(screen.getByText('Released')).toBeInTheDocument();
    expect(onSetReleased).toHaveBeenCalledWith(expect.objectContaining({ id: 'b' }), false);
  });

  it('says so when the server refuses', async () => {
    renderDialog([player('a', 'Ola')], vi.fn().mockRejectedValue({ status: 409 }));

    await userEvent.click(screen.getByRole('button', { name: 'Release the seat of Ola' }));

    expect(await screen.findByText(/Could not change that seat/)).toBeInTheDocument();
  });

  it('leaves the button enabled while who is connected is not known', () => {
    mocks.presence.mockReturnValue(undefined);
    renderDialog([player('a', 'Ola')]);

    expect(screen.getByRole('button', { name: 'Release the seat of Ola' })).toBeEnabled();
    expect(screen.queryByText('Online')).not.toBeInTheDocument();
  });
});
