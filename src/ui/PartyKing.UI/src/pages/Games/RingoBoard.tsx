import { CSSProperties, ReactNode, useMemo } from 'react';
import { PlayingCard } from './PlayingCard';
import { createLayout } from './ringoLayout';

type Props = {
  seed: number;
  flippedCards: number[];
  onFlip: (cardId: number) => void;
};

// Card width follows the smaller board side, so 52 cards fit the board on both a phone and a wide screen
const boardStyle = { '--card-w': 'min(11vw, 13vh)' } as CSSProperties;

/** The table: all 52 cards scattered at random positions, laid out from the game's seed. */
export const RingoBoard = ({ seed, flippedCards, onFlip }: Props): ReactNode => {
  const layout = useMemo(() => createLayout(seed), [seed]);
  const flipped = useMemo(() => new Set(flippedCards), [flippedCards]);

  return (
    <div
      data-testid="ringo-board"
      style={boardStyle}
      className="relative w-full h-full overflow-hidden rounded-lg bg-emerald-900 border border-emerald-700"
    >
      {layout.map((placement) => (
        <PlayingCard key={placement.id} placement={placement} flipped={flipped.has(placement.id)} onFlip={onFlip} />
      ))}
    </div>
  );
};
