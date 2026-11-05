import { ReactNode } from 'react';
import Tooltip from '@mui/material/Tooltip';
import { useTranslation } from 'react-i18next';
import { DefectorFlipTooltipContent, DefectorSwitchCardIcon, FIRST_FLIP_MISSION_INDEX } from './ResistanceIcons';

type Props = {
  text: string;
  /** Defector module: the switch cards flipped so far, true for Change Allegiance; the latest one is shown next to the text. */
  defectorFlips?: readonly boolean[];
};

/**
 * The large line that says what phase the game is in and what to do next; shown in every view. With the Defector module
 * the latest switch card sits beside it, so a player who is not looking at the board sees a flip too.
 */
export const ResistancePhaseBanner = ({ text, defectorFlips = [] }: Props): ReactNode => {
  const { t } = useTranslation();
  const latest = defectorFlips.at(-1);
  // The first flip belongs to mission 3, the next ones to the missions after it
  const missionNumber = FIRST_FLIP_MISSION_INDEX + defectorFlips.length;

  return (
    <div
      role="status"
      className="flex items-center justify-center gap-3 w-full px-5 py-3 rounded-xl border border-amber-400/40
        bg-gradient-to-r from-slate-900/90 via-slate-800/90 to-slate-900/90
        shadow-lg shadow-amber-500/10 ring-1 ring-inset ring-white/5"
    >
      <span aria-hidden="true" className="w-1.5 self-stretch rounded-full bg-gradient-to-b from-amber-300 to-amber-600" />
      <p className="flex-1 text-base sm:text-2xl lg:text-3xl font-semibold leading-snug text-center text-amber-50 tracking-wide drop-shadow">
        {text}
      </p>
      {latest !== undefined && (
        <Tooltip
          arrow
          placement="bottom"
          enterTouchDelay={0}
          leaveTouchDelay={4000}
          title={<DefectorFlipTooltipContent missionNumber={missionNumber} change={latest} />}
        >
          <span tabIndex={0} className="shrink-0 cursor-help outline-none">
            <DefectorSwitchCardIcon
              change={latest}
              className="h-12 w-auto sm:h-16 lg:h-20"
              title={t(latest ? 'resistance.defectorFlips.latestChange' : 'resistance.defectorFlips.latestNoChange', {
                mission: missionNumber,
              })}
            />
          </span>
        </Tooltip>
      )}
      <span aria-hidden="true" className="w-1.5 self-stretch rounded-full bg-gradient-to-b from-amber-300 to-amber-600" />
    </div>
  );
};
