import { ReactNode } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { MIN_PLAYERS } from './resistanceTeams';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto;
  isAdmin: boolean;
  onManagePlayers: () => void;
  onManageSeats: () => void;
  onEnd: () => void;
  onStart: () => void;
  onOpen: () => void;
  onRefreshRules: () => void;
  onContinue: () => void;
  onRestart: () => void;
  logHidden: boolean;
  onToggleLog: () => void;
};

const buttonClass =
  'px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700 cursor-pointer disabled:opacity-50 disabled:cursor-default';

/**
 * What the host and admins do to the game: manage the players, start, open it to guests (admin), free the seat of a
 * player who lost their device once it started, and end it.
 */
export const ResistanceManagerControls = ({ game, isAdmin, onManagePlayers, onManageSeats, onEnd, onStart, onOpen, onRefreshRules, onContinue, onRestart, logHidden, onToggleLog }: Props): ReactNode => {
  const { t } = useTranslation();
  const started = game.startedAt != null;
  const enough = game.players.length >= MIN_PLAYERS;

  const missionOver = game.phase === 'MissionResult';
  // A game waiting for the Assassin can be restarted too, so an absent Assassin never strands it
  const gameOver = game.phase === 'GameOver' || game.phase === 'Assassination';

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
      {started && (
        <button type="button" onClick={onManageSeats} title={t('resistance.controls.seatsHint')} className={buttonClass}>
          {t('resistance.controls.seats')}
        </button>
      )}
      {game.playedPlotCards.length > 0 && (
        <button type="button" onClick={onToggleLog} aria-pressed={logHidden} className={buttonClass}>
          {logHidden ? t('resistance.controls.showLog') : t('resistance.controls.hideLog')}
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
