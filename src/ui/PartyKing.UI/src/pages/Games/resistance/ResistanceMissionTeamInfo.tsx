import { ReactNode } from 'react';
import Tooltip from '@mui/material/Tooltip';
import { useTranslation } from 'react-i18next';

type Props = {
  /** Centre of the (i) badge in the board's viewBox. */
  cx: number;
  cy: number;
  missionNumber: number;
  succeeded: boolean;
  /** Names of the players who went on the mission. */
  names: readonly string[];
};

const TooltipContent = ({ missionNumber, succeeded, names }: Pick<Props, 'missionNumber' | 'succeeded' | 'names'>): ReactNode => {
  const { t } = useTranslation();

  return (
  <div className="px-1 py-0.5">
    <div className="mb-1 text-sm font-bold">
      {t('resistance.teamInfo.mission', { n: missionNumber })}
      <span className={succeeded ? 'text-emerald-300' : 'text-red-300'}>{succeeded ? t('resistance.teamInfo.succeeded') : t('resistance.teamInfo.failed')}</span>
    </div>
    <div className="mb-1 text-xs uppercase tracking-wide opacity-70">{t('resistance.teamInfo.team')}</div>
    <ul className="m-0 list-none p-0 text-sm">
      {names.map((name, i) => (
        <li key={i} className="py-0.5">
          {name}
        </li>
      ))}
    </ul>
  </div>
  );
};

/** An (i) badge for the mission board (SVG) that shows the finished mission's team in a tooltip. */
export const ResistanceMissionTeamInfo = ({ cx, cy, missionNumber, succeeded, names }: Props): ReactNode => {
  const { t } = useTranslation();

  return (
  <Tooltip
    arrow
    placement="bottom"
    enterTouchDelay={0}
    leaveTouchDelay={4000}
    title={<TooltipContent missionNumber={missionNumber} succeeded={succeeded} names={names} />}
  >
    <g
      role="img"
      tabIndex={0}
      aria-label={t('resistance.teamInfo.aria', { n: missionNumber, names: names.join(', ') })}
      style={{ cursor: 'help', outline: 'none' }}
    >
      <circle cx={cx} cy={cy} r={13} fill="#f5c542" stroke="#0f172a" strokeWidth="2.5" />
      <circle cx={cx} cy={cy - 6} r={2.2} fill="#0f172a" />
      <rect x={cx - 2} y={cy - 2} width={4} height={10} rx={1.5} fill="#0f172a" />
    </g>
  </Tooltip>
  );
};
