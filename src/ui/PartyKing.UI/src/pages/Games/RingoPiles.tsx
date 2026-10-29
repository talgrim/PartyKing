import { ReactNode } from 'react';
import { RingoFlipDto } from '@/service/endpoints/games/types';
import { KINGS_TO_END, pileCards } from './ringoCards';
import { RingoCard } from './ringoLayout';

type Props = {
  flips: RingoFlipDto[];
  pendingCardId: number | null;
};

const Pile = ({ label, cards }: { label: string; cards: RingoCard[] }): ReactNode => (
  <div className="flex items-center gap-1" aria-label={label}>
    <span className="mr-1 text-slate-300">{label}</span>
    {cards.map((card) => (
      <span
        key={card.id}
        className={`px-1.5 py-0.5 text-sm font-semibold bg-white rounded ${card.isRed ? 'text-red-600' : 'text-slate-900'}`}
      >
        {card.rank}
        {card.suit}
      </span>
    ))}
  </div>
);

/** The kings and aces that were found and put aside, so everyone sees how many are gone. */
export const RingoPiles = ({ flips, pendingCardId }: Props): ReactNode => {
  const { kings, aces } = pileCards(flips, pendingCardId);

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
      <Pile label={`Króle ${kings.length}/${KINGS_TO_END}`} cards={kings} />
      <Pile label={`Asy ${aces.length}`} cards={aces} />
    </div>
  );
};
