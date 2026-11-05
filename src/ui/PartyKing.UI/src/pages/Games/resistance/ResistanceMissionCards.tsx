import { ReactNode } from 'react';
import { CardBackIcon, MissionFailCardIcon, MissionReverseCardIcon, MissionSuccessCardIcon } from './ResistanceIcons';
import { ResistanceMissionCardKind } from '@/service/endpoints/games/types';
import { ResistanceTvPanel } from './ResistanceTvPanel';
import { useTranslation } from 'react-i18next';

type Props = {
  /** How many cards were played: the size of the team (the open card included). */
  cardCount: number;
  /** Fail cards among them (the open card included); null with the Reverser module, which keeps the number secret, so the cards stay face down. */
  failCount: number | null;
  /** The card played openly through "In the Spotlight": shown first and signed with the player's name. */
  open?: { name: string; card: ResistanceMissionCardKind } | null;
};

const CARD_SIZE = 'h-[clamp(5rem,16vh,11rem)] w-auto';

/**
 * The played mission cards of a finished mission, Fail cards first, for the TV view. Nothing tells who played which,
 * except the card an In the Spotlight forced open, which comes first with the player's name under it.
 */
export const ResistanceMissionCards = ({ cardCount, failCount, open = null }: Props): ReactNode => {
  const { t } = useTranslation();
  const hidden = open ? Math.max(cardCount - 1, 0) : cardCount;
  const hiddenFails = failCount == null ? 0 : open?.card === 'Fail' ? Math.max(failCount - 1, 0) : failCount;

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
        {Array.from({ length: hidden }, (_, i) => (
          <li key={i} className="flex w-[6vw] min-w-20 max-w-32 flex-col items-center">
            {failCount == null && <CardBackIcon title={t('resistance.missionCards.hidden')} className={CARD_SIZE} />}
            {failCount != null && i < hiddenFails && <MissionFailCardIcon title={t('resistance.missionCard.fail')} className={CARD_SIZE} />}
            {failCount != null && i >= hiddenFails && <MissionSuccessCardIcon title={t('resistance.missionCard.success')} className={CARD_SIZE} />}
          </li>
        ))}
      </ul>
    </ResistanceTvPanel>
  );
};
