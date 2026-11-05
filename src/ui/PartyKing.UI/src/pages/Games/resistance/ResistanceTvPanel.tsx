import { ReactNode } from 'react';

type Props = {
  /** The accessible name of the section, usually its title. */
  label: string;
  title: string;
  /** Colour of the title (a Tailwind text class). */
  titleClassName?: string;
  /** Tint of the panel's ring (a Tailwind ring class). */
  ringClassName?: string;
  children: ReactNode;
};

/**
 * One group of cards under the TV board (the team, the votes, the mission cards): a titled panel with its own background,
 * so the groups can sit close together and still read as separate. It may shrink below its content and wrap its cards
 * instead of being cut off.
 */
export const ResistanceTvPanel = ({
  label,
  title,
  titleClassName = 'text-slate-300',
  ringClassName = 'ring-slate-700',
  children,
}: Props): ReactNode => (
  <section
    aria-label={label}
    className={`flex min-w-0 max-w-full flex-col items-center gap-2 rounded-xl bg-slate-900/70 px-3 py-2 ring-1 ${ringClassName}`}
  >
    <h2 className={`text-[clamp(1.1rem,2.6vh,2rem)] font-semibold uppercase tracking-wide ${titleClassName}`}>
      {title}
    </h2>
    {children}
  </section>
);
