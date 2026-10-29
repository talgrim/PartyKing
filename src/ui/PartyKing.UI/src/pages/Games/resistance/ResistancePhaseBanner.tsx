import { ReactNode } from 'react';

type Props = {
  text: string;
};

/** The large line that says what phase the game is in and what to do next; shown in every view. */
export const ResistancePhaseBanner = ({ text }: Props): ReactNode => (
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
    <span aria-hidden="true" className="w-1.5 self-stretch rounded-full bg-gradient-to-b from-amber-300 to-amber-600" />
  </div>
);
