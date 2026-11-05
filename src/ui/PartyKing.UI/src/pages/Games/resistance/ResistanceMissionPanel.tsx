import { ReactNode, useState } from 'react';
import { ResistanceGameDto, ResistanceMissionCardKind } from '@/service/endpoints/games/types';
import { MissionFailCardIcon, MissionReverseCardIcon, MissionSuccessCardIcon } from './ResistanceIcons';
import { MISSION_CARD_KEYS } from './missionCardKeys';
import { useTranslation } from 'react-i18next';

type Props = {
  game: ResistanceGameDto;
  onPlay: (card: ResistanceMissionCardKind) => Promise<void>;
};

const card = (disabled: boolean) =>
  `h-[clamp(10rem,40vw,20rem)] w-auto flex-none rounded-xl transition ${
    disabled ? 'cursor-not-allowed opacity-40' : 'cursor-pointer opacity-80 hover:opacity-100'
  }`;

/**
 * A member of the approved team plays one secret mission card. A card can't be taken back, and a member of the
 * Resistance can only play Success, so the Fail card is disabled for them. With the Reverser module a Reverser also
 * holds a Reverse card; a spy Reverser can't play Fail.
 */
export const ResistanceMissionPanel = ({ game, onPlay }: Props): ReactNode => {
  const { t } = useTranslation();
  const [sending, setSending] = useState(false);
  const played = game.yourMissionCard != null;
  const reverser = game.yourCharacter === 'Reverser';
  const mayFail = game.yourRole === 'Spy' && !reverser;

  const play = async (card: ResistanceMissionCardKind) => {
    setSending(true);
    try {
      await onPlay(card);
    } finally {
      setSending(false);
    }
  };

  return (
    <section aria-label={t('resistance.missionPanel.label')} className="w-full flex flex-col gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="text-lg font-semibold text-slate-100">{t('resistance.missionPanel.title')}</h2>
        <span className="text-sm tabular-nums text-slate-400" aria-live="polite">
          {game.playedMissionPlayerIds.length}/{game.teamPlayerIds.length}
        </span>
      </div>
      {played ? (
        <p className="text-sm text-slate-300">
          {t('resistance.missionPanel.played', { card: t(MISSION_CARD_KEYS[game.yourMissionCard!]) })}
        </p>
      ) : (
        <>
          <div className="flex justify-center gap-4">
            <MissionSuccessCardIcon
              title={t('resistance.missionCard.success')}
              className={card(sending)}
              disabled={sending}
              onClick={() => void play('Success')}
            />
            <MissionFailCardIcon
              title={t('resistance.missionCard.fail')}
              className={card(sending || !mayFail)}
              disabled={sending || !mayFail}
              onClick={() => void play('Fail')}
            />
            {reverser && (
              <MissionReverseCardIcon
                title={t('resistance.missionCard.reverse')}
                className={card(sending)}
                disabled={sending}
                onClick={() => void play('Reverse')}
              />
            )}
          </div>
          <p className="text-sm text-slate-400">
            {t(game.rules.reverserEnabled ? 'resistance.missionPanel.hintReverser' : 'resistance.missionPanel.hint')}
          </p>
        </>
      )}
    </section>
  );
};
