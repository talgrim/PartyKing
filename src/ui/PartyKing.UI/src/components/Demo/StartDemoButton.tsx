import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useStartDemo } from './useStartDemo';

type Props = {
  /** The button's text; "Start the demo" by default. */
  label?: string;
};

/** Starts a demo session and opens it, and says why when that is not possible. */
export const StartDemoButton = ({ label }: Props): ReactNode => {
  const { t } = useTranslation();
  const { start, starting, error } = useStartDemo();

  return (
    <>
      <button
        onClick={() => void start()}
        disabled={starting}
        className="px-5 py-3 font-semibold text-slate-100 bg-amber-600 rounded-md cursor-pointer hover:bg-amber-500 disabled:opacity-50 disabled:cursor-default"
      >
        {starting ? t('demo.landing.starting') : (label ?? t('demo.landing.start'))}
      </button>
      {error && <p className="mt-3 text-sm text-amber-500">{error}</p>}
    </>
  );
};
