import { ReactNode } from 'react';
import { LeaderTokenIcon } from './ResistanceIcons';
import { ResistancePlayerDto } from '@/service/endpoints/games/types';
import { useTranslation } from 'react-i18next';

type Props = {
  /** Only the players on the team, in seating order. */
  members: ResistancePlayerDto[];
  missionNumber: number;
  leaderId: string | null;
};

/** The team the leader submitted, as a row of player cards under the mission board of the TV view. */
export const ResistanceTeamCards = ({ members, missionNumber, leaderId }: Props): ReactNode => {
  const { t } = useTranslation();

  return (
  <section aria-label={t('resistance.teamCards.label')} className="flex flex-col items-center gap-2 shrink-0">
    <h2 className="text-[clamp(1.1rem,2.6vh,2rem)] font-semibold uppercase tracking-wide text-emerald-300">{t('resistance.teamCards.title', { n: missionNumber })}</h2>
    <ul className="flex flex-wrap justify-center gap-3">
      {members.map((player) => (
        <li
          key={player.id}
          title={player.name}
          className="relative flex w-[13vw] min-w-32 max-w-60 flex-col items-center gap-2 rounded-xl bg-slate-900 px-3 pb-3 pt-4 ring-2 ring-emerald-400 shadow-lg shadow-emerald-400/20"
        >
          {player.id === leaderId && (
            <span className="absolute right-2 top-2">
              <LeaderTokenIcon title={t('resistance.leader')} />
            </span>
          )}
          <span
            aria-hidden="true"
            className="flex h-[7vh] min-h-12 w-[7vh] min-w-12 items-center justify-center rounded-full bg-emerald-400 text-[clamp(1.25rem,3vh,2.5rem)] font-bold text-slate-900"
          >
            {player.name.charAt(0).toUpperCase()}
          </span>
          <span className="max-w-full truncate text-[clamp(1rem,2.4vh,1.75rem)] font-medium text-slate-100">{player.name}</span>
        </li>
      ))}
    </ul>
  </section>
  );
};
