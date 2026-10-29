import { ReactNode } from 'react';
import { RingoFlipDto } from '@/service/endpoints/games/types';
import { cardEffect, cardName } from './ringoCards';
import { RingoRuleSuggestions } from './RingoRuleSuggestions';
import { getCard } from './ringoLayout';

type Props = {
  sessionId: string;
  cardId: number;
  flips: RingoFlipDto[];
  /** Rules suggested so far, kept by the page so they survive the overlay closing. */
  suggestedRules: string[];
  onSuggested: (rules: string[]) => void;
  onContinue: () => void;
};

/** The card that was just flipped, in the middle of the screen with its effect, until the host or admin continues. */
export const RingoCardOverlay = ({ sessionId, cardId, flips, suggestedRules, onSuggested, onContinue }: Props): ReactNode => {
  const card = getCard(cardId);
  const name = cardName(cardId);

  return (
    <div
      role="dialog"
      aria-label={name}
      className="absolute inset-0 z-50 flex flex-col items-center justify-center gap-4 p-4 bg-black/70 text-center"
    >
      <div
        className={`flex flex-col items-center justify-center w-40 aspect-5/7 rounded-lg bg-white border border-slate-300 shadow-xl text-5xl font-semibold leading-none ${
          card.isRed ? 'text-red-600' : 'text-slate-900'
        }`}
      >
        <span>{card.rank}</span>
        <span>{card.suit}</span>
      </div>
      <h2 className="text-3xl font-semibold text-slate-100">{name}</h2>
      <p className="max-w-xl text-lg text-slate-200">{cardEffect(cardId, flips)}</p>
      {card.rank === 'A' && (
        <RingoRuleSuggestions sessionId={sessionId} suggestedRules={suggestedRules} onSuggested={onSuggested} />
      )}
      <button
        type="button"
        onClick={onContinue}
        className="px-6 py-2 text-lg text-slate-100 bg-indigo-700 rounded-md cursor-pointer hover:bg-indigo-600"
      >
        Kontynuuj
      </button>
    </div>
  );
};
