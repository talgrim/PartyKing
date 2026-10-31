import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ResistanceMissionCards } from './ResistanceMissionCards';

const cardOf = (item: HTMLElement) => item.querySelector('[role="img"]')?.getAttribute('aria-label');

describe('ResistanceMissionCards', () => {
  it('shows the cards without names, Fail first', () => {
    render(<ResistanceMissionCards cardCount={3} failCount={1} />);

    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    expect(screen.queryByText('Ala')).toBeNull();
  });

  it('puts the openly played card first, signed with the player, and counts it out of the hidden ones', () => {
    render(<ResistanceMissionCards cardCount={3} failCount={1} open={{ name: 'Ala', success: false }} />);

    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveTextContent('Ala');
    expect(cardOf(items[0])).toBe('Fail');
    // the one Fail was the open card, so the two hidden cards are Successes
    expect(items.slice(1).map((item) => cardOf(item))).toEqual(['Success', 'Success']);
  });

  it('keeps the hidden Fail cards when the open card is a Success', () => {
    render(<ResistanceMissionCards cardCount={2} failCount={1} open={{ name: 'Bartek', success: true }} />);

    const items = screen.getAllByRole('listitem');
    expect(cardOf(items[0])).toBe('Success');
    expect(cardOf(items[1])).toBe('Fail');
  });
});
