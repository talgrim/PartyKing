import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { MIN_PLAYERS } from './resistanceTeams';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto;
  isAdmin: boolean;
  onManagePlayers: () => void;
  onEnd: () => void;
  onStart: () => void;
  onOpen: () => void;
  onRefreshRules: () => void;
  onContinue: () => void;
  onRestart: () => void;
};

const buttonClass =
  'px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700 cursor-pointer disabled:opacity-50 disabled:cursor-default';

/** What the host and admins do to the game: manage the players, start, open it to guests (admin) and end it. */
export const ResistanceManagerControls = ({ game, isAdmin, onManagePlayers, onEnd, onStart, onOpen, onRefreshRules, onContinue, onRestart }: Props): ReactNode => {
  const { t } = useTranslation();
  const started = game.startedAt != null;
  const enough = game.players.length >= MIN_PLAYERS;

  const missionOver = game.phase === 'MissionResult';
  const gameOver = game.phase === 'GameOver';

  return (
    <>
      {missionOver && (
        <button type="button" onClick={onContinue} className={buttonClass}>
          {t('resistance.next')}
        </button>
      )}
      {gameOver && (
        <button type="button" onClick={onRestart} className={buttonClass}>
          {t('resistance.controls.restart')}
        </button>
      )}
      {!started && (
        <button type="button" onClick={onManagePlayers} className={buttonClass}>
          {t('lobby.players')}
        </button>
      )}
      <button type="button" onClick={onEnd} className={buttonClass}>
        {t('resistance.controls.end')}
      </button>
      {game.isOpen && !started && (
        <button
          type="button"
          onClick={onStart}
          disabled={!enough}
          title={enough ? undefined : t('resistance.controls.startNeeds', { min: MIN_PLAYERS })}
          className={buttonClass}
        >
          {t('resistance.controls.start')}
        </button>
      )}
      {!started && (
        <button
          type="button"
          onClick={onRefreshRules}
          disabled={game.isOpen}
          title={game.isOpen ? t('resistance.controls.refreshRulesOpen') : t('resistance.controls.refreshRulesHint')}
          className={buttonClass}
        >
          {t('resistance.controls.refreshRules')}
        </button>
      )}
      {isAdmin && !started && (
        <button type="button" onClick={onOpen} disabled={game.isOpen} className={buttonClass}>
          {t('resistance.controls.open')}
        </button>
      )}
    </>
  );
};
