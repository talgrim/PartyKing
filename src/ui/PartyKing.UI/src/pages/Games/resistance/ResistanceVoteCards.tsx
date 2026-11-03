import { ReactNode } from 'react';
import { ResistancePlayerDto, ResistanceVoteDto } from '@/service/endpoints/games/types';
import { VoteApproveCardIcon, VoteRejectCardIcon } from './ResistanceIcons';
import { useTranslation } from 'react-i18next';

type Props = {
  players: ResistancePlayerDto[];
  votes: ResistanceVoteDto[];
};

/** The public votes of a finished vote, as one card per voter signed with the voter's name, for the TV view. */
export const ResistanceVoteCards = ({ players, votes }: Props): ReactNode => {
  const { t } = useTranslation();

  return (
  <section aria-label={t('resistance.voteCards.title')} className="flex flex-col items-center gap-2 shrink-0">
    <h2 className="text-[clamp(1.1rem,2.6vh,2rem)] font-semibold uppercase tracking-wide text-slate-300">{t('resistance.voteCards.title')}</h2>
    <ul className="flex flex-wrap justify-center gap-3">
      {players
        .map((player) => ({ player, vote: votes.find((vote) => vote.playerId === player.id) }))
        .filter((entry) => entry.vote !== undefined)
        .map(({ player, vote }) => (
          <li key={player.id} className="flex w-[8vw] min-w-20 max-w-36 flex-col items-center gap-1">
            {vote!.approve ? (
              <VoteApproveCardIcon title={t('resistance.vote.for')} className="h-[clamp(5rem,16vh,11rem)] w-auto" />
            ) : (
              <VoteRejectCardIcon title={t('resistance.vote.against')} className="h-[clamp(5rem,16vh,11rem)] w-auto" />
            )}
            <span className="max-w-full truncate text-[clamp(0.9rem,2.2vh,1.5rem)] font-medium text-slate-100">{player.name}</span>
          </li>
        ))}
    </ul>
  </section>
  );
};
