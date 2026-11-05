import { ReactNode } from 'react';
import { LeaderTokenIcon } from './ResistanceIcons';
import { ResistanceTvPanel } from './ResistanceTvPanel';
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
    <ResistanceTvPanel
      label={t('resistance.teamCards.label')}
      title={t('resistance.teamCards.title', { n: missionNumber })}
      titleClassName="text-emerald-300"
      ringClassName="ring-emerald-400/40"
    >
      <ul className="flex flex-1 flex-wrap content-center justify-center gap-2">
        {members.map((player) => (
          <li
            key={player.id}
            title={player.name}
            className="relative flex w-[7vw] min-w-24 max-w-40 flex-col items-center gap-2 rounded-xl bg-slate-900 px-2 pb-2 pt-3 ring-2 ring-emerald-400 shadow-lg shadow-emerald-400/20"
          >
            {player.id === leaderId && (
              <span className="absolute right-1.5 top-1.5">
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
    </ResistanceTvPanel>
  );
};
