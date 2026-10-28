import { ReactNode } from 'react';

export type ErrorLogEntry = {
  id: number;
  time: Date;
  message: string;
};

type Props = {
  entries: ErrorLogEntry[];
};

export const ErrorLog = ({ entries }: Props): ReactNode => (
  <section aria-label="Error log" className="max-h-40 overflow-y-auto p-3 bg-slate-900 rounded-lg text-xs font-mono">
    <h2 className="pb-1 font-semibold text-slate-500">Error log</h2>
    {entries.length === 0 ? (
      <p className="text-slate-500">No errors</p>
    ) : (
      <ul className="space-y-1">
        {[...entries].reverse().map((entry) => (
          <li key={entry.id} className="text-amber-500">
            <time dateTime={entry.time.toISOString()} className="pr-2 text-slate-500">
              {entry.time.toLocaleTimeString()}
            </time>
            {entry.message}
          </li>
        ))}
      </ul>
    )}
  </section>
);
