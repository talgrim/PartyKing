import { ReactNode, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { DemoSessionDto } from '@/service/endpoints/demo/types';
import { CountdownLevel, formatCountdown } from './demoCountdown';
import { useDemoCountdown } from './useDemoCountdown';
import { helpTarget } from '@/components/Help/helpSteps';

const LEVEL_STYLES: Record<CountdownLevel, string> = {
  normal: 'border-slate-800',
  soon: 'border-amber-600',
  last: 'border-red-500',
};

const TIME_STYLES: Record<CountdownLevel, string> = {
  normal: 'text-slate-100',
  soon: 'text-amber-500',
  last: 'text-red-400',
};

type Props = {
  demo: DemoSessionDto;
  /** Called when the time is up. */
  onEnded: () => void;
};

/**
 * Tells everyone in a demo session that it is one, and when it ends. The last minutes are shown as a warning. It owns
 * the ticking clock, so the page around it does not render every second.
 */
export const DemoBanner = ({ demo, onEnded }: Props): ReactNode => {
  const { t } = useTranslation();
  const countdown = useDemoCountdown(demo);
  const ended = countdown?.ended ?? false;

  useEffect(() => {
    if (ended) onEnded();
  }, [ended, onEnded]);

  if (!countdown) return null;

  const { level, remainingMs } = countdown;

  return (
    <div {...helpTarget('demoBanner')} className={`mx-2 mt-4 p-3 bg-slate-900 border rounded-lg ${LEVEL_STYLES[level]}`}>
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-semibold tracking-wider text-amber-500 uppercase">{t('demo.banner.label')}</span>
        <span className={`font-mono ${TIME_STYLES[level]}`}>
          {t('demo.banner.endsIn', { time: formatCountdown(remainingMs) })}
        </span>
      </div>
      {/* Its text changes only when the warning starts, so it is the part that a screen reader announces */}
      <p role="status" className="mt-1 text-sm text-slate-500">
        {level === 'normal' ? t('demo.banner.about') : t('demo.banner.soon')}
      </p>
    </div>
  );
};
