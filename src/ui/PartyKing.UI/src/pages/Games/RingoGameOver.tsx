import { ReactNode } from 'react';
import { RingoFlipDto } from '@/service/endpoints/games/types';
import { cardEffect, cardName } from './ringoCards';
import { getCard } from './ringoLayout';

type Props = {
  /** The flip of the fourth king. */
  flip: RingoFlipDto;
  flips: RingoFlipDto[];
  onEnd: () => void;
};

/** Shown instead of the card overlay once the fourth king is flipped: the game is over and says who ended it. */
export const RingoGameOver = ({ flip, flips, onEnd }: Props): ReactNode => {
  const card = getCard(flip.cardId);

  return (
    <div
      role="dialog"
      aria-label="Koniec gry"
      className="absolute inset-0 z-50 flex flex-col items-center justify-center gap-4 p-4 bg-black/80 text-center"
    >
      <h2 className="text-4xl font-bold text-amber-400">Koniec gry!</h2>
      <div
        className={`flex flex-col items-center justify-center w-32 aspect-5/7 rounded-lg bg-white border border-slate-300 shadow-xl text-4xl font-semibold leading-none ${
          card.isRed ? 'text-red-600' : 'text-slate-900'
        }`}
      >
        <span>{card.rank}</span>
        <span>{card.suit}</span>
      </div>
      <p className="text-2xl text-slate-100">
        <strong>{flip.flippedBy}</strong> odwrócił/a czwartego króla: {cardName(flip.cardId)}
      </p>
      <p className="max-w-xl text-lg text-slate-200">{cardEffect(flip.cardId, flips)}</p>
      <button
        type="button"
        onClick={onEnd}
        className="px-6 py-2 text-lg text-slate-100 bg-indigo-700 rounded-md cursor-pointer hover:bg-indigo-600"
      >
        Zakończ grę
      </button>
    </div>
  );
};
