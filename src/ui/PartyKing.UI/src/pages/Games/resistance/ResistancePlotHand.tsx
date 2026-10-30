import { ReactNode } from 'react';
import { ResistancePlotCardDto } from '@/service/endpoints/games/types';
import { PLOT_CARDS } from './resistancePlotCards';

type Props = {
  cards: ResistancePlotCardDto[];
};

/** The plot cards a player holds, privately. Cards waiting to be dealt out are shown by the dealer instead. */
export const ResistancePlotHand = ({ cards }: Props): ReactNode => {
  const held = cards.filter((card) => !card.toDeal);

  if (held.length === 0) return null;

  return (
    <section aria-label="Twoje karty Intryg" className="w-full flex flex-col gap-2 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700">
      <h2 className="text-lg font-semibold text-slate-100">Twoje karty Intryg</h2>
      <ul className="flex flex-col gap-3">
        {held.map((card) => {
          const { name, effect, Icon } = PLOT_CARDS[card.type];

          return (
            <li key={card.id} className="flex items-center gap-3">
              <Icon className="w-16 shrink-0" title={name} />
              <div className="min-w-0">
                <p className="font-medium text-slate-100">{name}</p>
                <p className="text-sm text-slate-400">{effect}</p>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
};
