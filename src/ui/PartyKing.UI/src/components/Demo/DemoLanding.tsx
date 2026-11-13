import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { MyDemoSessions } from './MyDemoSessions';
import { StartDemoButton } from './StartDemoButton';

/**
 * The landing page of a demo server: no list of everybody's sessions, but the visitor's own ones that are still
 * running, and a button that starts a new one.
 */
export const DemoLanding = (): ReactNode => {
  const { t } = useTranslation();

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="w-full max-w-xl mx-auto pt-6">
        <div className="mx-2 mt-12 p-4 bg-slate-900 rounded-lg">
          <h1 className="text-2xl font-semibold text-slate-100">{t('demo.landing.title')}</h1>
          <p className="mt-2">{t('demo.landing.intro')}</p>
          <p className="mt-2 text-sm text-slate-500">{t('demo.landing.noSpotify')}</p>
          <div className="mt-4">
            <StartDemoButton />
          </div>
          <p className="mt-3 text-sm text-slate-500">{t('demo.landing.hint')}</p>
        </div>
        <MyDemoSessions />
      </div>
    </div>
  );
};
