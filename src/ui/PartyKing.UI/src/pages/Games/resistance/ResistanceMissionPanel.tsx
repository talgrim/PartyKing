import { ReactNode, useState } from 'react';
import { ResistanceGameDto } from '@/service/endpoints/games/types';
import { MissionFailCardIcon, MissionSuccessCardIcon } from './ResistanceIcons';

type Props = {
  game: ResistanceGameDto;
  onPlay: (success: boolean) => Promise<void>;
};

const card = (disabled: boolean) =>
  `h-[clamp(10rem,40vw,20rem)] w-auto flex-none rounded-xl transition ${
    disabled ? 'cursor-not-allowed opacity-40' : 'cursor-pointer opacity-80 hover:opacity-100'
  }`;

/**
 * A member of the approved team plays one secret mission card. A card can't be taken back, and a member of the
 * Resistance can only play Success, so the Fail card is disabled for them.
 */
export const ResistanceMissionPanel = ({ game, onPlay }: Props): ReactNode => {
  const [sending, setSending] = useState(false);
  const played = game.yourMissionCard != null;
  const mayFail = game.yourRole === 'Spy';

  const play = async (success: boolean) => {
    setSending(true);
    try {
      await onPlay(success);
    } finally {
      setSending(false);
    }
  };

  return (
    <section aria-label="Misja" className="w-full flex flex-col gap-3 rounded-xl bg-slate-900 p-4 ring-1 ring-slate-700 shadow-lg">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="text-lg font-semibold text-slate-100">Zagraj kartę misji</h2>
        <span className="text-sm tabular-nums text-slate-400" aria-live="polite">
          {game.playedMissionPlayerIds.length}/{game.teamPlayerIds.length}
        </span>
      </div>
      {played ? (
        <p className="text-sm text-slate-300">
          Zagrano kartę: {game.yourMissionCard ? 'Sukces' : 'Porażka'}. Czekamy na pozostałych członków drużyny.
        </p>
      ) : (
        <>
          <div className="flex justify-center gap-4">
            <MissionSuccessCardIcon
              title="Sukces"
              className={card(sending)}
              disabled={sending}
              onClick={() => void play(true)}
            />
            <MissionFailCardIcon
              title="Porażka"
              className={card(sending || !mayFail)}
              disabled={sending || !mayFail}
              onClick={() => void play(false)}
            />
          </div>
          <p className="text-sm text-slate-400">
            Karta jest tajna, wszyscy zobaczą tylko liczbę kart Porażka. Zagranej karty nie można zmienić.
          </p>
        </>
      )}
    </section>
  );
};
