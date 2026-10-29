import { CSSProperties, ReactNode, useMemo } from 'react';
import { PlayingCard } from './PlayingCard';
import { createLayout } from './ringoLayout';

type Props = {
  seed: number;
  /** The card flipped last and not continued past yet: it stays face up, and no other card can be flipped meanwhile. */
  pendingCardId: number | null;
  /** The fourth king was flipped: nothing can be flipped any more. */
  gameOver?: boolean;
  /** Ids of every flipped card; the ones but the pending one already left the table. */
  flippedCards: number[];
  onFlip: (cardId: number) => void;
};

// Card width follows the smaller board side, so 52 cards fit the board on both a phone and a wide screen
const boardStyle = { '--card-w': 'min(11vw, 13vh)' } as CSSProperties;

/** The table: the cards still in play scattered at random positions, laid out from the game's seed. */
export const RingoBoard = ({ seed, pendingCardId, gameOver = false, flippedCards, onFlip }: Props): ReactNode => {
  const layout = useMemo(() => createLayout(seed), [seed]);
  const flipped = useMemo(() => new Set(flippedCards), [flippedCards]);

  return (
    <div
      data-testid="ringo-board"
      style={boardStyle}
      className="relative w-full h-full overflow-hidden rounded-lg bg-emerald-900 border border-emerald-700"
    >
      {layout
        .filter((placement) => !flipped.has(placement.id) || placement.id === pendingCardId)
        .map((placement) => (
          <PlayingCard
            key={placement.id}
            placement={placement}
            flipped={flipped.has(placement.id)}
            disabled={pendingCardId !== null || gameOver}
            onFlip={onFlip}
          />
        ))}
    </div>
  );
};
