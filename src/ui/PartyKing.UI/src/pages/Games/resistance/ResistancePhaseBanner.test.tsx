import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { ResistancePhaseBanner } from './ResistancePhaseBanner';

describe('ResistancePhaseBanner', () => {
  it('shows the text and no switch card without flips', () => {
    render(<ResistancePhaseBanner text="Pick the team" />);

    expect(screen.getByRole('status')).toHaveTextContent('Pick the team');
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('shows only the latest switch card, named after the mission it was flipped for', () => {
    render(<ResistancePhaseBanner text="Pick the team" defectorFlips={[true, false]} />);

    expect(screen.getAllByRole('img')).toHaveLength(1);
    expect(screen.getByRole('img', { name: 'Latest switch card (mission 4): No Change' })).toBeInTheDocument();
  });

  it('explains the card in a tooltip on hover', async () => {
    render(<ResistancePhaseBanner text="Pick the team" defectorFlips={[true]} />);

    await userEvent.hover(screen.getByRole('img', { name: 'Latest switch card (mission 3): Change Allegiance' }));

    expect(await screen.findByText('Switch card: both Defectors changed sides. Nobody is told who they are.')).toBeInTheDocument();
    expect(screen.getByText('Mission 3: Change Allegiance')).toBeInTheDocument();
  });
});
