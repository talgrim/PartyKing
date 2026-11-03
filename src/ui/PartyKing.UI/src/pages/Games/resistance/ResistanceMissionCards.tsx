import { ReactNode } from 'react';
import { MissionFailCardIcon, MissionSuccessCardIcon } from './ResistanceIcons';
import { useTranslation } from 'react-i18next';

type Props = {
  /** How many cards were played: the size of the team (the open card included). */
  cardCount: number;
  /** Fail cards among them (the open card included). */
  failCount: number;
  /** The card played openly through "In the Spotlight": shown first and signed with the player's name. */
  open?: { name: string; success: boolean } | null;
};

const CARD_SIZE = 'h-[clamp(5rem,16vh,11rem)] w-auto';

/**
 * The played mission cards of a finished mission, Fail cards first, for the TV view. Nothing tells who played which,
 * except the card an In the Spotlight forced open, which comes first with the player's name under it.
 */
export const ResistanceMissionCards = ({ cardCount, failCount, open = null }: Props): ReactNode => {
  const { t } = useTranslation();
  const hidden = open ? Math.max(cardCount - 1, 0) : cardCount;
  const hiddenFails = open && !open.success ? Math.max(failCount - 1, 0) : failCount;

  return (
    <section aria-label={t('resistance.missionCards.title')} className="flex flex-col items-center gap-2 shrink-0">
      <h2 className="text-[clamp(1.1rem,2.6vh,2rem)] font-semibold uppercase tracking-wide text-slate-300">{t('resistance.missionCards.title')}</h2>
      <ul className="flex flex-wrap justify-center gap-3">
        {open && (
          <li className="flex w-[8vw] min-w-20 max-w-36 flex-col items-center gap-1">
            {open.success ? (
              <MissionSuccessCardIcon title={t('resistance.missionCard.success')} className={CARD_SIZE} />
            ) : (
              <MissionFailCardIcon title={t('resistance.missionCard.fail')} className={CARD_SIZE} />
            )}
            <span className="max-w-full truncate text-[clamp(0.9rem,2.2vh,1.5rem)] font-medium text-slate-100">{open.name}</span>
          </li>
        )}
        {Array.from({ length: hidden }, (_, i) => (
          <li key={i} className="flex w-[8vw] min-w-20 max-w-36 flex-col items-center">
            {i < hiddenFails ? (
              <MissionFailCardIcon title={t('resistance.missionCard.fail')} className={CARD_SIZE} />
            ) : (
              <MissionSuccessCardIcon title={t('resistance.missionCard.success')} className={CARD_SIZE} />
            )}
          </li>
        ))}
      </ul>
    </section>
  );
};
