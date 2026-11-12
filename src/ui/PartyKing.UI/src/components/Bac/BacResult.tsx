import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { BacEstimate } from './bacCalculation';
import { BacUnit, formatBac } from './bacUnits';

type Props = {
  estimate: BacEstimate;
  unit: BacUnit;
  now: number;
  /** Whether Watson's total body water, rather than the average Widmark factor, was used. */
  totalBodyWater: boolean;
};

const MS_PER_MINUTE = 60_000;
const MINUTES_PER_HOUR = 60;

/** The estimate: now, the peak, when the blood is clear and which legal limits are passed. */
export const BacResult = ({ estimate, unit, now, totalBodyWater }: Props): ReactNode => {
  const { t, i18n } = useTranslation();
  const language = i18n.language;
  const clock = (epochMs: number) => new Date(epochMs).toLocaleTimeString(language, { hour: '2-digit', minute: '2-digit' });

  const minutesToSober = estimate.soberAt === null ? 0 : Math.ceil((estimate.soberAt - now) / MS_PER_MINUTE);

  return (
    <section className="space-y-2 p-3 bg-slate-900/60 rounded-md" aria-labelledby="bac-result">
      <h3 id="bac-result" className="font-semibold text-slate-100">
        {t('bac.result')}
      </h3>
      <p className="text-3xl font-semibold text-amber-500" aria-live="polite">
        {formatBac(estimate.nowPromille, unit, language)} {unit.symbol}
      </p>
      <dl className="grid grid-cols-2 gap-x-3 text-sm text-slate-300">
        {estimate.peakAt !== null && (
          <>
            <dt>{t('bac.peak')}</dt>
            <dd>
              {formatBac(estimate.peakPromille, unit, language)} {unit.symbol} ({clock(estimate.peakAt)})
            </dd>
          </>
        )}
        <dt>{t('bac.sober')}</dt>
        <dd>
          {estimate.soberAt === null
            ? t('bac.alreadySober')
            : t('bac.soberAt', {
                time: clock(estimate.soberAt),
                hours: Math.floor(minutesToSober / MINUTES_PER_HOUR),
                minutes: minutesToSober % MINUTES_PER_HOUR,
              })}
        </dd>
      </dl>
      <ul className="text-sm space-y-0.5">
        {unit.limits.map((limit) => {
          const exceeded = estimate.nowPromille >= limit.promille;
          return (
            <li key={limit.labelKey} className={exceeded ? 'text-red-400' : 'text-slate-400'}>
              {t(limit.labelKey)}: {formatBac(limit.promille, unit, language)} {unit.symbol}
              {exceeded && ` – ${t('bac.exceeded')}`}
            </li>
          );
        })}
      </ul>
      <p className="text-xs text-slate-500">{totalBodyWater ? t('bac.modelWatson') : t('bac.modelWidmark')}</p>
    </section>
  );
};
