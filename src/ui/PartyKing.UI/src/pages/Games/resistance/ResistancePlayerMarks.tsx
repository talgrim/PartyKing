import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';

type Props = {
  game: ResistanceGameDto;
  playerId: string;
};

const badge = 'shrink-0 text-xs uppercase rounded px-1';

/**
 * What the table may know about a player: whether they have voted (not how) while the vote is on, how they voted once
 * all have, and their role once the game is over.
 */
export const ResistancePlayerMarks = ({ game, playerId }: Props): ReactNode => {
  const votesShown = game.phase === 'SelectingTeam' || game.phase === 'PlayingMission';
  const vote = votesShown ? game.votes?.find((x) => x.playerId === playerId) : undefined;
  const role = game.revealedRoles?.find((x) => x.playerId === playerId)?.role;
  const maker = game.opinionMakerPlayerIds.includes(playerId);
  // A revealed Opinion Maker vote replaces the plain "voted" tick while the vote is on
  const makerVote = game.phase === 'Voting' ? game.opinionMakerVotes.find((x) => x.playerId === playerId) : undefined;
  const waiting = game.phase === 'Voting' && game.votedPlayerIds.includes(playerId) && makerVote == null;

  const cardCount = game.plotCardCounts.find((x) => x.playerId === playerId)?.count ?? 0;

  const played = game.phase === 'PlayingMission' && game.playedMissionPlayerIds.includes(playerId);

  return (
    <>
      {maker && (
        <span title="Kreator opinii" className={`${badge} bg-amber-400 text-slate-900`}>
          Kreator opinii
        </span>
      )}
      {makerVote && (
        <span className={`${badge} ${makerVote.approve ? 'bg-emerald-400 text-slate-900' : 'bg-rose-400 text-slate-900'}`}>
          {makerVote.approve ? 'Za' : 'Przeciw'}
        </span>
      )}
      {cardCount > 0 && (
        <span title={`Karty Intryg: ${cardCount}`} className={`${badge} bg-amber-700 text-white`}>
          <span aria-hidden="true">🂠 {cardCount}</span>
          <span className="sr-only">Karty Intryg: {cardCount}</span>
        </span>
      )}
      {played && (
        <span title="Zagrał/a kartę" className={`${badge} bg-slate-700 text-slate-200`}>
          <span aria-hidden="true">✓</span>
          <span className="sr-only">Zagrał/a kartę</span>
        </span>
      )}
      {waiting && (
        <span title="Zagłosował/a" className={`${badge} bg-slate-700 text-slate-200`}>
          <span aria-hidden="true">✓</span>
          <span className="sr-only">Zagłosował/a</span>
        </span>
      )}
      {vote && (
        <span className={`${badge} ${vote.approve ? 'bg-emerald-400 text-slate-900' : 'bg-rose-400 text-slate-900'}`}>
          {vote.approve ? 'Za' : 'Przeciw'}
        </span>
      )}
      {role && (
        <span className={`${badge} ${role === 'Spy' ? 'bg-rose-700 text-white' : 'bg-sky-700 text-white'}`}>
          {role === 'Spy' ? 'Szpieg' : 'Ruch Oporu'}
        </span>
      )}
    </>
  );
};
