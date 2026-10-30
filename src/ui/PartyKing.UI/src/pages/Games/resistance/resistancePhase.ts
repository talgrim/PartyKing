import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { IMMEDIATE_PLOT_CARDS } from './resistancePlotCards';
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

    const results = game.missionResults ?? [];
    const succeeded = results.filter((result) => result.succeeded).length;
    const score = `Ruch Oporu ${succeeded}, Szpiedzy ${results.length - succeeded}`;

    if (game.phase === 'GameOver') {
      const why =
        game.winner === 'Spy'
          ? game.rejectedTeams >= MAX_REJECTED_TEAMS
            ? `${MAX_REJECTED_TEAMS} drużyn z rzędu zostało odrzuconych`
            : 'trzy misje się nie powiodły'
          : 'trzy misje zakończyły się sukcesem';

      return `Koniec gry! ${game.winner === 'Spy' ? 'Szpiedzy wygrali' : 'Ruch Oporu wygrał'}: ${why} (${score}). Wszystkie role zostały ujawnione.`;
    }

    if (game.phase === 'DealingCards') {
      const task = youLead
        ? 'rozdaj dobrane karty Intryg innym graczom'
        : 'rozdaje dobrane karty Intryg innym graczom';

      return `${who} (korona): ${task}. Potem wybierze drużynę na ${mission} misję.`;
    }

    if (game.phase === 'LeaderWindow') {
      const passed = game.leaderWindowPassedPlayerIds.length;
      const mine = game.yourPlayerId == null
        ? ''
        : game.leaderWindowPassedPlayerIds.includes(game.yourPlayerId)
          ? ' Twoje potwierdzenie jest zapisane.'
          : ' Potwierdź, że nie zagrywasz karty (albo zagraj ją).';

      return `${who} (korona), ale zanim cokolwiek zrobi, gracze decydują, czy zagrywają karty Intrygi. Potwierdzili: ${passed}/${count}.${mine}`;
    }

    if (game.phase === 'ConfidenceWindow') {
      const passed = game.leaderWindowPassedPlayerIds.length;
      const mine = game.yourPlayerId == null
        ? ''
        : game.leaderWindowPassedPlayerIds.includes(game.yourPlayerId)
          ? ' Twoje potwierdzenie jest zapisane.'
          : ' Potwierdź, że nie zagrywasz karty (albo zagraj ją).';

      return `Drużyna (${names.join(', ')}) została zatwierdzona (${tally}), ale zanim wyruszy, gracze decydują, czy zagrywają karty Intrygi. Potwierdzili: ${passed}/${count}.${mine}`;
    }

    if (game.phase === 'ResolvingCards') {
      const waiting = game.playedPlotCards
        .filter((card) => IMMEDIATE_PLOT_CARDS.includes(card.type) && !card.isConfirmed)
        .map((card) => game.players.find((player) => player.id === (card.type === 'OverheardConversation' ? card.holderPlayerId : card.targetPlayerId))?.name)
        .filter((name) => name != null);
      const confirm = waiting.length > 0 ? ` Czekamy na potwierdzenie: ${waiting.join(', ')}.` : '';

      return `${who} (korona) rozdał/a karty Intryg. Gracze zagrywają karty natychmiastowe i potwierdzają, że zobaczyli kartę Tożsamości; potem drużynę wybierze lider.${confirm}`;
    }

    if (game.phase === 'PlayingMission') {
      const progress = `Zagrano ${game.playedMissionPlayerIds.length}/${game.teamPlayerIds.length} kart.`;
      const fails = game.requiredFails ?? 1;
      const rule = fails > 1 ? ` Misja kończy się porażką od ${fails} kart Porażka.` : '';
      const onTeam = game.yourPlayerId != null && game.teamPlayerIds.includes(game.yourPlayerId);
      const mine = !onTeam
        ? ''
        : game.yourMissionCard == null
          ? ' Zagraj swoją kartę misji.'
          : ' Twoja karta jest zagrana.';

      const openName = game.players.find((player) => player.id === game.openMissionPlayerId)?.name;
      const open = openName == null
        ? ''
        : game.openMissionCard == null
          ? ` ${openName} musi zagrać kartę misji jawnie („Pod obserwacją”).`
          : ` ${openName} zagrał/a kartę jawnie: ${game.openMissionCard ? 'Sukces' : 'Porażka'}.`;

      return `Drużyna (${names.join(', ')}) została zatwierdzona (${tally}) i wykonuje ${mission} misję. ${progress}${rule}${mine}${open}`;
    }

    if (game.phase === 'MissionResult') {
      const last = results.find((result) => result.missionNumber === game.missionNumber);
      const next = youLead || viewer.canManage ? 'Kliknij „Dalej”' : 'Lider lub host kliknie „Dalej”';
      const outcome = last?.succeeded
        ? 'Misja zakończyła się sukcesem'
        : `Misja zakończyła się porażką (kart Porażka: ${last?.failCount ?? 0})`;
      const fails = last?.succeeded && last.failCount > 0 ? ` (kart Porażka: ${last.failCount})` : '';

      return `${outcome}${fails}. Wynik: ${score}. ${next}, żeby zaczęła się kolejna misja.`;
    }

    if (game.phase === 'Voting') {
      const progress = `Zagłosowało ${game.votedPlayerIds.length}/${count}.`;
      const mine =
        game.yourPlayerId == null
          ? ''
          : game.yourVote == null
            ? ' Zagłosuj: zatwierdź albo odrzuć drużynę.'
            : ' Twój głos możesz zmienić, dopóki nie zagłosują wszyscy.';

      const makers = game.players.filter((player) => game.opinionMakerPlayerIds.includes(player.id));
      const pendingMakers = makers.filter((player) => !game.votedPlayerIds.includes(player.id));
      const order =
        pendingMakers.length > 0
          ? ` Najpierw głosuje kreator opinii: ${pendingMakers.map((player) => player.name).join(', ')}.`
          : '';

      return `${who} (korona) zgłosił/a drużynę na ${mission} misję: ${names.join(', ')}. Teraz drużyna idzie pod głosowanie. ${progress}${order}${mine}${card}`;
    }

    const task = youLead
      ? `wybierz ${size} osób na ${mission} misję i zgłoś je do głosowania`
      : `wybiera ${size} osób na ${mission} misję`;

    if (votes.length > 0) {
      return `Poprzednia drużyna została odrzucona (${tally}).${rejected} ${who} (korona): ${task}. Omówcie to na żywo.${card}`;
    }

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
