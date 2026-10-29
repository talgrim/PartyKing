import { CSSProperties, ReactNode } from 'react';
import { CardPlacement, getCard } from './ringoLayout';

type Props = {
  placement: CardPlacement;
  flipped: boolean;
  onFlip: (cardId: number) => void;
};

/** One card on the board: face down until it is clicked, then it stays face up where it lies. */
export const PlayingCard = ({ placement, flipped, onFlip }: Props): ReactNode => {
  const card = getCard(placement.id);

  // The board sets --card-w; the card stays inside it, whatever the board size
  const style: CSSProperties = {
    left: `calc((100% - var(--card-w)) * ${placement.x})`,
    top: `calc((100% - var(--card-w) * 1.4) * ${placement.y})`,
    transform: `rotate(${placement.rotation}deg)`,
    zIndex: placement.z,
  };

  const label = `${card.rank}${card.suit}`;

  return (
    <button
      type="button"
      aria-label={flipped ? label : 'Face down card'}
      onClick={() => !flipped && onFlip(card.id)}
      style={style}
      className={`absolute w-(--card-w) aspect-5/7 rounded-md border shadow-md select-none ${
        flipped
          ? `bg-white border-slate-300 cursor-default ${card.isRed ? 'text-red-600' : 'text-slate-900'}`
          : 'border-slate-200 bg-linear-to-br from-indigo-700 to-indigo-950 cursor-pointer hover:brightness-125'
      }`}
    >
      {flipped && (
        <span className="flex flex-col items-center justify-center h-full font-semibold leading-none text-[length:calc(var(--card-w)*0.3)]">
          <span>{card.rank}</span>
          <span>{card.suit}</span>
        </span>
      )}
    </button>
  );
};
