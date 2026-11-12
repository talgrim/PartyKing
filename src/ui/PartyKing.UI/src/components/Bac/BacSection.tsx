import { ReactNode, useId } from 'react';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

type Props = {
  title: string;
  expanded: boolean;
  onToggle: () => void;
  /** A section that cannot be opened: it stays shut whatever `expanded` says. */
  disabled?: boolean;
  /** A line beside the title, like the reason a section cannot be opened. */
  note?: string;
  children: ReactNode;
};

/**
 * A section of the calculator that opens and shuts from its header, which is a button across the whole width with the
 * arrow at its right end. The content stays mounted while it is shut (only hidden), so what is half typed in it is still
 * there when it opens again.
 */
export const BacSection = ({ title, expanded, onToggle, disabled = false, note, children }: Props): ReactNode => {
  const id = useId();
  const open = expanded && !disabled;
  const Arrow = open ? ExpandLessIcon : ExpandMoreIcon;

  return (
    <section className="overflow-hidden rounded-lg border border-slate-700">
      <h3 className="font-semibold text-slate-100">
        <button
          type="button"
          aria-expanded={open}
          aria-controls={`${id}-panel`}
          aria-labelledby={`${id}-title`}
          aria-describedby={note ? `${id}-note` : undefined}
          disabled={disabled}
          onClick={onToggle}
          className="flex w-full items-center gap-2 bg-slate-700 px-3 py-2 text-left cursor-pointer hover:bg-slate-600 disabled:cursor-default disabled:text-slate-500 disabled:hover:bg-slate-700"
        >
          <span id={`${id}-title`}>{title}</span>
          {note && (
            <span id={`${id}-note`} className="min-w-0 truncate text-xs font-normal text-slate-400">
              {note}
            </span>
          )}
          <Arrow fontSize="small" className="ml-auto shrink-0" />
        </button>
      </h3>
      <div id={`${id}-panel`} role="region" aria-labelledby={`${id}-title`} hidden={!open} className="bg-slate-900/30 p-3">
        {children}
      </div>
    </section>
  );
};
