import { ReactNode } from 'react';
import { ResistancePlayedPlotCardDto, ResistancePlayerDto } from '@/service/endpoints/games/types';
import { i18n } from '@/i18n';
import { PLOT_CARDS } from './resistancePlotCards';
import { useTranslation } from 'react-i18next';

type Props = {
  cards: ResistancePlayedPlotCardDto[];
  players: ResistancePlayerDto[];
};

const LOG_TYPES = [
  'OverheardConversation',
  'EstablishConfidence',
  'OpenUp',
  'TakeResponsibility',
  'KeepingCloseEye',
  'StrongLeader',
  'NoConfidence',
] as const;

const describe = (card: ResistancePlayedPlotCardDto, holder: string, target: string): string => {
  const { name } = PLOT_CARDS[card.type];
  const type = LOG_TYPES.find((x) => x === card.type);

  return type
    ? i18n.t(`resistance.log.${type}`, { holder, name, target })
    : i18n.t('resistance.log.default', { holder, name });
};

/** The public log of the played immediate plot cards, newest first. Who saw what stays with the receiver. */
export const ResistancePlotLog = ({ cards, players }: Props): ReactNode => {
  const { t } = useTranslation();
  if (cards.length === 0) return null;

  const nameOf = (id: string) => players.find((player) => player.id === id)?.name ?? t('resistance.playerFallback');

  return (
    <section aria-label={t('resistance.log.title')} className="w-full rounded-xl bg-slate-900 p-3 ring-1 ring-slate-700">
      <h2 className="mb-1 text-sm font-semibold text-slate-300">{t('resistance.log.title')}</h2>
      <ul className="flex flex-col gap-1 text-sm text-slate-200">
        {cards.map((card) => (
          <li key={card.id}>
            <span className="text-slate-500">{t('resistance.log.mission', { n: card.missionNumber })}</span>
            {describe(card, nameOf(card.holderPlayerId), card.targetPlayerId ? nameOf(card.targetPlayerId) : '')}
            {!card.isConfirmed && <span className="text-amber-300">{t('resistance.log.pending')}</span>}
          </li>
        ))}
      </ul>
    </section>
  );
};
