import { ReactNode } from 'react';
import { MissionFailCardIcon, MissionSuccessCardIcon } from './ResistanceIcons';

type Props = {
  /** How many cards were played: the size of the team. */
  cardCount: number;
  failCount: number;
};

/** The played mission cards of a finished mission, Fail cards first; nothing tells who played which, for the TV view. */
export const ResistanceMissionCards = ({ cardCount, failCount }: Props): ReactNode => (
  <section aria-label="Mission cards" className="flex flex-col items-center gap-2 shrink-0">
    <h2 className="text-[clamp(1.1rem,2.6vh,2rem)] font-semibold uppercase tracking-wide text-slate-300">Karty misji</h2>
    <ul className="flex flex-wrap justify-center gap-3">
      {Array.from({ length: cardCount }, (_, i) => (
        <li key={i} className="flex w-[8vw] min-w-20 max-w-36 flex-col items-center">
          {i < failCount ? (
            <MissionFailCardIcon title="Porażka" className="h-[clamp(5rem,16vh,11rem)] w-auto" />
          ) : (
            <MissionSuccessCardIcon title="Sukces" className="h-[clamp(5rem,16vh,11rem)] w-auto" />
          )}
        </li>
      ))}
    </ul>
  </section>
);
