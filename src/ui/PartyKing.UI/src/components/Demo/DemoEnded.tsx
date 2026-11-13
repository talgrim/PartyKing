import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { StartDemoButton } from './StartDemoButton';

/** Shown instead of a demo session once its time is up: the server deletes it, so there is nothing left to show. */
export const DemoEnded = (): ReactNode => {
  const { t } = useTranslation();

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="w-full max-w-xl mx-auto pt-6">
        <div role="status" className="mx-2 mt-12 p-4 bg-slate-900 rounded-lg">
          <h1 className="text-2xl font-semibold text-slate-100">{t('demo.ended.title')}</h1>
          <p className="mt-2">{t('demo.ended.text')}</p>
          <div className="mt-4">
            <StartDemoButton label={t('demo.ended.restart')} />
          </div>
        </div>
      </div>
    </div>
  );
};
