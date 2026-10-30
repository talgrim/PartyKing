import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { ResistancePlayerMarks } from './ResistancePlayerMarks';
import { ResistanceVotePanel } from './ResistanceVotePanel';

const players = ['Ala', 'Bartek', 'Celina'].map((name, i) => ({ id: `p${i}`, name }));

const game = (extra: Partial<ResistanceGameDto>): ResistanceGameDto =>
  ({
    players,
    phase: 'Voting',
    yourPlayerId: 'p1',
    yourVote: null,
    votedPlayerIds: [],
    votes: null,
    revealedRoles: null,
    playedMissionPlayerIds: [],
    plotCardCounts: [],
    opinionMakerPlayerIds: ['p0'],
    opinionMakerVotes: [],
    ...extra,
  }) as unknown as ResistanceGameDto;

describe('ResistanceVotePanel with an Opinion Maker', () => {
  it('keeps the others waiting until the Opinion Maker has voted', () => {
    render(<ResistanceVotePanel game={game({})} onVote={vi.fn()} />);

    expect(screen.getByText('Najpierw głosuje kreator opinii: Ala.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Zatwierdź' })).toHaveAttribute('aria-disabled', 'true');
  });

  it('lets the others vote once the Opinion Maker has', async () => {
    const onVote = vi.fn().mockResolvedValue(undefined);
    render(<ResistanceVotePanel game={game({ votedPlayerIds: ['p0'] })} onVote={onVote} />);

    await userEvent.setup().click(screen.getByRole('button', { name: 'Zatwierdź' }));

    expect(onVote).toHaveBeenCalledWith(true);
  });

  it('lets the Opinion Maker vote first and locks the vote afterwards', () => {
    const { rerender } = render(<ResistanceVotePanel game={game({ yourPlayerId: 'p0' })} onVote={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Odrzuć' })).not.toHaveAttribute('aria-disabled', 'true');

    rerender(
      <ResistanceVotePanel game={game({ yourPlayerId: 'p0', yourVote: true, votedPlayerIds: ['p0'] })} onVote={vi.fn()} />,
    );
    expect(screen.getByRole('button', { name: 'Odrzuć' })).toHaveAttribute('aria-disabled', 'true');
  });
});

describe('ResistancePlayerMarks with an Opinion Maker', () => {
  it('shows the Opinion Maker badge and, once revealed, their vote', () => {
    render(
      <ResistancePlayerMarks
        game={game({ votedPlayerIds: ['p0'], opinionMakerVotes: [{ playerId: 'p0', approve: false }] })}
        playerId="p0"
      />,
    );

    expect(screen.getByText('Kreator opinii')).toBeInTheDocument();
    expect(screen.getByText('Przeciw')).toBeInTheDocument();
  });

  it('shows nothing about the vote of a player who is not an Opinion Maker', () => {
    render(<ResistancePlayerMarks game={game({ votedPlayerIds: ['p2'] })} playerId="p2" />);

    expect(screen.queryByText('Kreator opinii')).not.toBeInTheDocument();
    expect(screen.queryByText('Za')).not.toBeInTheDocument();
  });
});
