import { render, screen } from '@testing-library/react';
import { RingoPlayers } from './RingoPlayers';
import { seatPositions } from './ringoSeats';

describe('RingoPlayers', () => {
  it('lists the players in turn order and marks the one on turn', () => {
    const players = [
      { id: 'a', name: 'Ola' },
      { id: 'b', name: 'Bartek' },
    ];
    render(<RingoPlayers players={players} currentPlayerId="b" yourPlayerId="a" />);

    const items = screen.getAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual(['Ola (you)*', 'Bartek']);
    expect(items[1]).toHaveAttribute('aria-current', 'true');
    expect(items[0]).not.toHaveAttribute('aria-current');
  });
});

describe('seatPositions', () => {
  it('keeps seats inside the page, below the navigation, and gives each its own spot', () => {
    const seats = seatPositions(8);

    expect(new Set(seats.map((seat) => `${seat.left}/${seat.top}`)).size).toBe(8);
    for (const seat of seats) {
      expect(seat.top).toBeGreaterThanOrEqual(15);
      expect(seat.top).toBeLessThanOrEqual(95);
      expect(seat.left).toBeGreaterThanOrEqual(5);
      expect(seat.left).toBeLessThanOrEqual(95);
    }
  });
});
