import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

export type ErrorLogEntry = {
  id: number;
  time: Date;
  message: string;
};

type Props = {
  entries: ErrorLogEntry[];
};

export const ErrorLog = ({ entries }: Props): ReactNode => {
  const { t, i18n } = useTranslation();

  return (
    <section aria-label={t('errorLog.title')} className="max-h-40 overflow-y-auto p-3 bg-slate-900 rounded-lg text-xs font-mono">
      <h2 className="pb-1 font-semibold text-slate-500">{t('errorLog.title')}</h2>
      {entries.length === 0 ? (
        <p className="text-slate-500">{t('errorLog.none')}</p>
      ) : (
        <ul className="space-y-1">
          {[...entries].reverse().map((entry) => (
            <li key={entry.id} className="text-amber-500">
              <time dateTime={entry.time.toISOString()} className="pr-2 text-slate-500">
                {entry.time.toLocaleTimeString(i18n.language)}
              </time>
              {entry.message}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
};
