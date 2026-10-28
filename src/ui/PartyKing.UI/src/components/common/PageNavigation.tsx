import { ReactNode } from 'react';
import { Link } from 'react-router-dom';

type NavigationItem = { label: string } & ({ to: string } | { onClick: () => void });

type Props = {
  links: NavigationItem[];
  className?: string;
};

const itemClass = 'px-3 py-2 text-slate-200 bg-slate-900 rounded-md hover:bg-slate-700';

/** Row of buttons: a link to another page, or an action (`onClick`) such as opening a dialog. */
export const PageNavigation = ({ links, className = '' }: Props): ReactNode => {
  return (
    <nav className={`flex gap-2 ${className}`}>
      {links.map((link) =>
        'to' in link ? (
          <Link key={link.label} to={link.to} className={itemClass}>
            {link.label}
          </Link>
        ) : (
          <button key={link.label} type="button" onClick={link.onClick} className={`${itemClass} cursor-pointer`}>
            {link.label}
          </button>
        ),
      )}
    </nav>
  );
};
