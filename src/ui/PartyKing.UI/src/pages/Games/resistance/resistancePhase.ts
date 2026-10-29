import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { MIN_PLAYERS } from './resistanceTeams';

const MAX_REJECTED_TEAMS = 5;

type Viewer = {
  /** Host or admin. */
  canManage: boolean;
  isAdmin: boolean;
};

/**
 * What is going on in the game and what has to happen next, for the line above the mission board. It never mentions
 * the viewer's role, because it stays on the screen while the role card is face up.
 */
export const resistancePhaseDescription = (
  game: ResistanceGameDto,
  viewer: Viewer,
  firstTeamSize: number,
): string => {
  const count = game.players.length;
  const missing = MIN_PLAYERS - count;

  if (game.startedAt != null) {
    const leader = game.players.find((player) => player.id === game.leaderPlayerId);
    const youLead = leader != null && leader.id === game.yourPlayerId;
    const who = youLead ? 'Ty jesteś liderem' : `Liderem jest ${leader?.name ?? 'jeden z graczy'}`;
    const size = game.requiredTeamSize ?? firstTeamSize;
    const mission = game.missionNumber === 1 ? 'pierwszą' : `${game.missionNumber}.`;
    const card = game.yourPlayerId != null ? ' Dotknij swojej karty, żeby poznać swoją rolę.' : '';

    const names = game.players.filter((player) => game.teamPlayerIds.includes(player.id)).map((player) => player.name);
    const rejected = game.rejectedTeams > 0 ? ` Odrzucone drużyny z rzędu: ${game.rejectedTeams}/${MAX_REJECTED_TEAMS}.` : '';
    const votes = game.votes ?? [];
    const approvals = votes.filter((vote) => vote.approve).length;
    const tally = `za: ${approvals}, przeciw: ${votes.length - approvals}`;

    if (game.phase === 'GameOver') {
      return `Koniec gry! ${
        game.winner === 'Spy'
          ? `Szpiedzy wygrali: ${MAX_REJECTED_TEAMS} drużyn z rzędu zostało odrzuconych`
          : 'Ruch Oporu wygrał'
      } (${tally}). Wszystkie role zostały ujawnione.`;
    }

    if (game.phase === 'VoteResult') {
      if (game.voteApproved) {
        return `Drużyna (${names.join(', ')}) została zatwierdzona (${tally}). Misja nie jest jeszcze gotowa w aplikacji.`;
      }

      const next = youLead || viewer.canManage ? 'Kliknij „Dalej”' : 'Lider lub host kliknie „Dalej”';

      return `Drużyna (${names.join(', ')}) została odrzucona (${tally}).${rejected} ${next}, żeby przywództwo przeszło na następnego gracza.`;
    }

    if (game.phase === 'Voting') {
      const progress = `Zagłosowało ${game.votedPlayerIds.length}/${count}.`;
      const mine =
        game.yourPlayerId == null
          ? ''
          : game.yourVote == null
            ? ' Zagłosuj: zatwierdź albo odrzuć drużynę.'
            : ' Twój głos możesz zmienić, dopóki nie zagłosują wszyscy.';

      return `${who} (korona) zgłosił/a drużynę na ${mission} misję: ${names.join(', ')}. Teraz drużyna idzie pod głosowanie. ${progress}${mine}${card}`;
    }

    const task = youLead
      ? `wybierz ${size} osób na ${mission} misję i zgłoś je do głosowania`
      : `wybiera ${size} osób na ${mission} misję`;

    return `Gra się rozpoczęła, nowi gracze nie mogą już dołączyć. ${who} (korona): ${task}. Omówcie to na żywo.${rejected}${card}`;
  }

  if (!game.isOpen) {
    const need = missing > 0 ? ` Potrzeba co najmniej ${MIN_PLAYERS} graczy.` : '';

    return viewer.isAdmin
      ? `Gra jest zamknięta. Otwórz ją dla gości, żeby gracze mogli dołączyć.${need}`
      : `Gra jest zamknięta. Poczekaj, aż admin otworzy ją dla gości.${need}`;
  }

  if (missing > 0) {
    const waiting = viewer.canManage ? 'Czekamy na graczy' : 'Czekamy na pozostałych graczy';

    return `${waiting}. Potrzeba co najmniej ${MIN_PLAYERS} graczy, brakuje jeszcze ${missing}. Pokazane rozmiary drużyn dotyczą gry w ${MIN_PLAYERS} osób.`;
  }

  return viewer.canManage
    ? 'Jest wystarczająco graczy. Gdy wszyscy dołączą, kliknij „Start game” (po starcie nikt nie dołączy). Role zostaną rozdane losowo.'
    : 'Jest wystarczająco graczy. Czekamy, aż host lub admin rozpocznie grę.';
};
