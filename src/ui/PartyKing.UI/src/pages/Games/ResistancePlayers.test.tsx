import { render, screen, within } from '@testing-library/react';
import { ResistancePlayers } from './ResistancePlayers';

const players = [
  { id: 'a', name: 'Ola', isSeatReleased: false, isManagerSeat: false },
  { id: 'b', name: 'Bartek', isSeatReleased: false, isManagerSeat: false },
  { id: 'c', name: 'Celina', isSeatReleased: false, isManagerSeat: false },
];

const seat = (name: string) => screen.getAllByRole('listitem').find((item) => item.getAttribute('title') === name)!;

describe('ResistancePlayers, connection status', () => {
  it('shows no status while who is connected is not known', () => {
    render(<ResistancePlayers players={players} yourPlayerId={null} />);

    expect(screen.queryByText('Online')).not.toBeInTheDocument();
    expect(screen.queryByText('Offline')).not.toBeInTheDocument();
    expect(seat('Ola')).not.toHaveClass('opacity-50');
  });

  it('marks who is online with a dot and dims the seats of the others', () => {
    render(<ResistancePlayers players={players} yourPlayerId={null} onlinePlayerIds={['a', 'c']} />);

    expect(within(seat('Ola')).getByText('Online')).toBeInTheDocument();
    expect(within(seat('Celina')).getByText('Online')).toBeInTheDocument();
    expect(within(seat('Bartek')).getByText('Offline')).toBeInTheDocument();
    expect(seat('Bartek')).toHaveClass('opacity-50');
    expect(seat('Ola')).not.toHaveClass('opacity-50');
  });

  it('treats a player missing from the list as offline, an empty list included', () => {
    render(<ResistancePlayers players={players} yourPlayerId={null} onlinePlayerIds={[]} />);

    expect(screen.getAllByText('Offline')).toHaveLength(3);
  });

  it('hides the dot from assistive technology and leaves the status as text', () => {
    render(<ResistancePlayers players={players} yourPlayerId={null} onlinePlayerIds={['a']} />);

    const dot = seat('Ola').querySelector('[data-online="true"]');
    expect(dot).toHaveAttribute('aria-hidden', 'true');
    expect(screen.getAllByText('Offline')[0]).toHaveClass('sr-only');
  });
});
