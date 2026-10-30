import { ReactNode } from 'react';
import { ResistancePlayedPlotCardDto, ResistancePlayerDto } from '@/service/endpoints/games/types';
import { PLOT_CARDS } from './resistancePlotCards';

type Props = {
  cards: ResistancePlayedPlotCardDto[];
  players: ResistancePlayerDto[];
};

const describe = (card: ResistancePlayedPlotCardDto, holder: string, target: string): string => {
  const { name } = PLOT_CARDS[card.type];
  if (card.type === 'OverheardConversation') return `${holder} zagrał/a „${name}” i ogląda kartę Tożsamości gracza ${target}`;
  if (card.type === 'EstablishConfidence') return `${holder} (przywódca) zagrał/a „${name}” i ujawnia swoją kartę Tożsamości graczowi ${target}`;
  if (card.type === 'OpenUp') return `${holder} zagrał/a „${name}” i ujawnia swoją kartę Tożsamości graczowi ${target}`;
  return `${holder} zagrał/a „${name}”`;
};

/** The public log of the played immediate plot cards, newest first. Who saw what stays with the receiver. */
export const ResistancePlotLog = ({ cards, players }: Props): ReactNode => {
  if (cards.length === 0) return null;

  const nameOf = (id: string) => players.find((player) => player.id === id)?.name ?? 'gracz';

  return (
    <section aria-label="Zagrane karty Intryg" className="w-full rounded-xl bg-slate-900 p-3 ring-1 ring-slate-700">
      <h2 className="mb-1 text-sm font-semibold text-slate-300">Zagrane karty Intryg</h2>
      <ul className="flex flex-col gap-1 text-sm text-slate-200">
        {cards.map((card) => (
          <li key={card.id}>
            <span className="text-slate-500">Misja {card.missionNumber}: </span>
            {describe(card, nameOf(card.holderPlayerId), card.targetPlayerId ? nameOf(card.targetPlayerId) : '')}
            {!card.isConfirmed && <span className="text-amber-300"> (czeka na potwierdzenie)</span>}
          </li>
        ))}
      </ul>
    </section>
  );
};
