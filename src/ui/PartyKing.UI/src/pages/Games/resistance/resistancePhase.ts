import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { MIN_PLAYERS } from './resistanceTeams';

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
    const task = youLead
      ? `wybierz ${firstTeamSize} osób na pierwszą misję`
      : `wybiera ${firstTeamSize} osób na pierwszą misję`;
    const card = game.yourPlayerId != null ? ' Dotknij swojej karty, żeby poznać swoją rolę.' : '';

    return `Gra się rozpoczęła, nowi gracze nie mogą już dołączyć. ${who} (korona): ${task}. Omówcie to na żywo.${card}`;
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
