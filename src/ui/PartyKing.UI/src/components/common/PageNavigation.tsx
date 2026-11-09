import { ReactNode, useState } from 'react';
import { Link } from 'react-router-dom';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';

type MenuEntry = { label: string; onClick: () => void };

type NavigationItem = { label: string } & (
  | { to: string; newTab?: boolean }
  | { onClick: () => void }
  | { menu: MenuEntry[] }
);

type Props = {
  links: NavigationItem[];
  className?: string;
};

const itemClass = 'px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700';

/** A button that drops a menu of actions; choosing one closes it. */
const NavigationMenu = ({ label, entries }: { label: string; entries: MenuEntry[] }): ReactNode => {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  const choose = (entry: MenuEntry) => {
    setAnchor(null);
    entry.onClick();
  };

  return (
    <>
      <button
        type="button"
        onClick={(event) => setAnchor(event.currentTarget)}
        aria-haspopup="menu"
        className={`${itemClass} cursor-pointer`}
      >
        {label}
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

/**
 * Row of buttons: a link to another page (in a new tab with `newTab`), an action (`onClick`) such as opening a dialog,
 * or a dropdown of actions (`menu`).
 */
export const PageNavigation = ({ links, className = '' }: Props): ReactNode => {
  return (
    <nav className={`flex gap-2 ${className}`}>
      {links.map((link) => {
        if ('to' in link) {
          return (
            <Link
              key={link.label}
              to={link.to}
              className={itemClass}
              {...(link.newTab ? { target: '_blank', rel: 'noopener noreferrer' } : {})}
            >
              {link.label}
            </Link>
          );
        }

        if ('menu' in link) return <NavigationMenu key={link.label} label={link.label} entries={link.menu} />;

        return (
          <button key={link.label} type="button" onClick={link.onClick} className={`${itemClass} cursor-pointer`}>
            {link.label}
          </button>
        );
      })}
    </nav>
  );
};
