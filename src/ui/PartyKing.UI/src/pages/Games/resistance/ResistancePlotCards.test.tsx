import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ResistancePlotCardDto } from '@/service/endpoints/games/types';
import { ResistancePlotDealer } from './ResistancePlotDealer';
import { ResistancePlotHand } from './ResistancePlotHand';

const players = [
  { id: 'p0', name: 'Ala' },
  { id: 'p1', name: 'Bartek' },
  { id: 'p2', name: 'Celina' },
];

const card = (id: number, type: ResistancePlotCardDto['type'], toDeal = false, inPlay = false): ResistancePlotCardDto => ({
  id,
  type,
  toDeal,
  inPlay,
});

describe('ResistancePlotHand', () => {
  it('lists the held cards and leaves out the ones waiting to be dealt', () => {
    render(<ResistancePlotHand cards={[card(1, 'NoConfidence'), card(2, 'OpenUp', true)]} />);

    expect(screen.getByText('No Confidence')).toBeInTheDocument();
    expect(screen.queryByText('Open Up')).not.toBeInTheDocument();
  });

  it('marks an Opinion Maker in play as visible to everyone', () => {
    render(<ResistancePlotHand cards={[card(5, 'OpinionMaker', false, true)]} />);

    expect(screen.getByText('In play, visible to everyone.')).toBeInTheDocument();
  });

  it('renders nothing without held cards', () => {
    const { container } = render(<ResistancePlotHand cards={[card(2, 'OpenUp', true)]} />);

    expect(container).toBeEmptyDOMElement();
  });
});

describe('ResistancePlotDealer', () => {
  it('offers every player but the leader and enables the deal only when each card has a recipient', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(
      <ResistancePlotDealer
        leaderId="p0"
        players={players}
        cards={[card(3, 'NoConfidence', true), card(4, 'StrongLeader', true)]}
        onSubmit={onSubmit}
      />,
    );

    const button = screen.getByRole('button', { name: 'Deal the cards' });
    expect(button).toBeDisabled();
    expect(screen.queryByRole('option', { name: 'Ala' })).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText(/Who gets the “No Confidence” card/), 'p1');
    expect(button).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/Who gets the “Strong Leader” card/), 'p1');
    expect(button).toBeEnabled();

    await user.click(button);

    expect(onSubmit).toHaveBeenCalledWith([
      { cardId: 3, playerId: 'p1' },
      { cardId: 4, playerId: 'p1' },
    ]);
  });
});
