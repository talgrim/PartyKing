import { ReactNode } from 'react';
import { MissionFailCardIcon, MissionReverseCardIcon, MissionSuccessCardIcon } from './ResistanceIcons';
import { ResistanceTvPanel } from './ResistanceTvPanel';
import { ResistanceMissionCardKind } from '@/service/endpoints/games/types';
import { useTranslation } from 'react-i18next';

type Props = {
  /** How many cards were played: the size of the team (the open card included). */
  cardCount: number;
  /** Fail cards among them (the open card included). */
  failCount: number;
  /** Reverse cards among them (the open card included); only with the Reverser module. */
  reverseCount?: number;
  /** The card played openly through "In the Spotlight": shown first and signed with the player's name. */
  open?: { name: string; card: ResistanceMissionCardKind } | null;
};

const CARD_SIZE = 'h-[clamp(5rem,16vh,11rem)] w-auto';

/** The kinds of the cards that are not the open one: Fail cards first, then Reverse, then Success. */
const hiddenKinds = (
  cardCount: number,
  failCount: number,
  reverseCount: number,
  open: ResistanceMissionCardKind | null,
): ResistanceMissionCardKind[] => {
  const total = open ? Math.max(cardCount - 1, 0) : cardCount;
  const fails = Math.min(open === 'Fail' ? Math.max(failCount - 1, 0) : failCount, total);
  const reverses = Math.min(open === 'Reverse' ? Math.max(reverseCount - 1, 0) : reverseCount, total - fails);

  return [
    ...Array.from({ length: fails }, (): ResistanceMissionCardKind => 'Fail'),
    ...Array.from({ length: reverses }, (): ResistanceMissionCardKind => 'Reverse'),
    ...Array.from({ length: total - fails - reverses }, (): ResistanceMissionCardKind => 'Success'),
  ];
};

/**
 * The played mission cards of a finished mission for the TV view, as when they are revealed shuffled at the table:
 * Fail cards first, then Reverse, then Success. Nothing tells who played which, except the card an In the Spotlight
 * forced open, which comes first with the player's name under it.
 */
export const ResistanceMissionCards = ({ cardCount, failCount, reverseCount = 0, open = null }: Props): ReactNode => {
  const { t } = useTranslation();
  const cards = hiddenKinds(cardCount, failCount, reverseCount, open?.card ?? null);

  return (
    <ResistanceTvPanel label={t('resistance.missionCards.title')} title={t('resistance.missionCards.title')}>
      <ul className="flex flex-1 flex-wrap content-center justify-center gap-2">
        {open && (
          <li className="flex w-[6vw] min-w-20 max-w-32 flex-col items-center gap-1">
            {open.card === 'Success' && <MissionSuccessCardIcon title={t('resistance.missionCard.success')} className={CARD_SIZE} />}
            {open.card === 'Fail' && <MissionFailCardIcon title={t('resistance.missionCard.fail')} className={CARD_SIZE} />}
            {open.card === 'Reverse' && <MissionReverseCardIcon title={t('resistance.missionCard.reverse')} className={CARD_SIZE} />}
            <span className="max-w-full truncate text-[clamp(0.9rem,2.2vh,1.5rem)] font-medium text-slate-100">{open.name}</span>
          </li>
        )}
        {cards.map((card, i) => (
          <li key={i} className="flex w-[6vw] min-w-20 max-w-32 flex-col items-center">
            {card === 'Fail' && <MissionFailCardIcon title={t('resistance.missionCard.fail')} className={CARD_SIZE} />}
            {card === 'Reverse' && <MissionReverseCardIcon title={t('resistance.missionCard.reverse')} className={CARD_SIZE} />}
            {card === 'Success' && <MissionSuccessCardIcon title={t('resistance.missionCard.success')} className={CARD_SIZE} />}
          </li>
        ))}
      </ul>
    </ResistanceTvPanel>
  );
};
