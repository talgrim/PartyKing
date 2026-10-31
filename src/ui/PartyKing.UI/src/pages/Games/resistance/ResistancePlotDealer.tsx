import { ReactNode, useState } from 'react';
import { ResistancePlayerDto, ResistancePlotCardDto } from '@/service/endpoints/games/types';
import { PLOT_CARDS } from './resistancePlotCards';
import { useTranslation } from 'react-i18next';

type Props = {
  /** The leader's id: the recipients are the other players. */
  leaderId: string;
  players: ResistancePlayerDto[];
  /** The drawn cards to hand out (`toDeal`). */
  cards: ResistancePlotCardDto[];
  onSubmit: (assignments: { cardId: number; playerId: string }[]) => Promise<void>;
};

/**
 * The leader's deal. Each drawn card gets a recipient (several cards may go to the same player); nothing is sent
 * until "Deal the cards", so the leader can change their mind.
 */
export const ResistancePlotDealer = ({ leaderId, players, cards, onSubmit }: Props): ReactNode => {
  const { t } = useTranslation();
  const [picked, setPicked] = useState<Record<number, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const recipients = players.filter((player) => player.id !== leaderId);
  const complete = cards.every((card) => picked[card.id] != null);

  const submit = async () => {
    setSubmitting(true);
    try {
      await onSubmit(cards.map((card) => ({ cardId: card.id, playerId: picked[card.id] })));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section aria-label={t('resistance.dealer.title')} className="w-full flex flex-col gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <h2 className="text-lg font-semibold text-slate-100">{t('resistance.dealer.title')}</h2>

      <ul className="flex flex-col gap-3">
        {cards.map((card) => {
          const { name, effect, Icon } = PLOT_CARDS[card.type];
          const selectId = `plot-recipient-${card.id}`;

          return (
            <li key={card.id} className="flex items-center gap-3">
              <Icon className="w-16 shrink-0" title={name} />
              <div className="min-w-0 flex-1">
                <p className="font-medium text-slate-100">{name}</p>
                <p className="mb-1 text-sm text-slate-400">{effect}</p>
                <label htmlFor={selectId} className="sr-only">
                  {t('resistance.dealer.to', { name })}
                </label>
                <select
                  id={selectId}
                  value={picked[card.id] ?? ''}
                  disabled={submitting}
                  onChange={(event) => setPicked((current) => ({ ...current, [card.id]: event.target.value }))}
                  className="w-full rounded-lg bg-slate-800 px-3 py-2 text-slate-100 ring-1 ring-slate-700"
                >
                  <option value="" disabled>
                    {t('resistance.dealer.pick')}
                  </option>
                  {recipients.map((player) => (
                    <option key={player.id} value={player.id}>
                      {player.name}
                    </option>
                  ))}
                </select>
              </div>
            </li>
          );
        })}
      </ul>

      <button
        type="button"
        disabled={!complete || submitting}
        onClick={() => void submit()}
        className="mt-1 rounded-lg bg-emerald-500 px-4 py-3 font-semibold text-slate-900 shadow transition-colors hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
      >
        {t('resistance.dealer.submit')}
      </button>
    </section>
  );
};
