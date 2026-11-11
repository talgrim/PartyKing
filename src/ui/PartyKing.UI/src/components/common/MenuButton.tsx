import { ReactNode, useState } from 'react';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';

export type MenuEntry = { label: string; onClick: () => void };

type Props = {
  label: string;
  /** Shown to the right of the label; decorative, so the button is still named by the label. */
  icon?: ReactNode;
  entries: MenuEntry[];
  className?: string;
};

/** A button that drops a menu of actions; choosing one closes the menu and runs it. */
export const MenuButton = ({ label, icon, entries, className }: Props): ReactNode => {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  const choose = (entry: MenuEntry) => {
    setAnchor(null);
    entry.onClick();
  };

  return (
    <>
      <button type="button" onClick={(event) => setAnchor(event.currentTarget)} aria-haspopup="menu" className={className}>
        <span className="truncate">{label}</span>
        {icon}
      </button>
      <Menu
        anchorEl={anchor}
        open={anchor !== null}
        onClose={() => setAnchor(null)}
        // MUI's emotion styles are unlayered and beat Tailwind's layered utilities, hence the important modifier
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        {entries.map((entry) => (
          <MenuItem key={entry.label} onClick={() => choose(entry)}>
            {entry.label}
          </MenuItem>
        ))}
      </Menu>
    </>
  );
};
