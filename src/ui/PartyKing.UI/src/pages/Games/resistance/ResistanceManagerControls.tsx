import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { MIN_PLAYERS } from './resistanceTeams';

type Props = {
  game: ResistanceGameDto;
  isAdmin: boolean;
  onManagePlayers: () => void;
  onEnd: () => void;
  onStart: () => void;
  onOpen: () => void;
  onRefreshRules: () => void;
  onContinue: () => void;
};

const buttonClass =
  'px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700 cursor-pointer disabled:opacity-50 disabled:cursor-default';

/** What the host and admins do to the game: manage the players, start, open it to guests (admin) and end it. */
export const ResistanceManagerControls = ({ game, isAdmin, onManagePlayers, onEnd, onStart, onOpen, onRefreshRules, onContinue }: Props): ReactNode => {
  const started = game.startedAt != null;
  const enough = game.players.length >= MIN_PLAYERS;

  const missionOver = game.phase === 'MissionResult';

  return (
    <>
      {missionOver && (
        <button type="button" onClick={onContinue} className={buttonClass}>
          Dalej
        </button>
      )}
      {!started && (
        <button type="button" onClick={onManagePlayers} className={buttonClass}>
          Players
        </button>
      )}
      <button type="button" onClick={onEnd} className={buttonClass}>
        End game
      </button>
      {game.isOpen && !started && (
        <button
          type="button"
          onClick={onStart}
          disabled={!enough}
          title={enough ? undefined : `Potrzeba co najmniej ${MIN_PLAYERS} graczy`}
          className={buttonClass}
        >
          Start game
        </button>
      )}
      {!started && (
        <button
          type="button"
          onClick={onRefreshRules}
          disabled={game.isOpen}
          title={game.isOpen ? 'Zasady można odświeżyć tylko przed otwarciem gry' : 'Pobierz aktualną konfigurację zasad z serwera'}
          className={buttonClass}
        >
          Refresh rules
        </button>
      )}
      {isAdmin && !started && (
        <button type="button" onClick={onOpen} disabled={game.isOpen} className={buttonClass}>
          Open the game
        </button>
      )}
    </>
  );
};
