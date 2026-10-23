import { ReactNode } from 'react';
import { Link } from 'react-router-dom';

type Props = {
  links: { to: string; label: string }[];
  className?: string;
};

export const PageNavigation = ({ links, className = '' }: Props): ReactNode => {
  return (
    <nav className={`flex gap-2 ${className}`}>
      {links.map((link) => (
        <Link
          key={link.to}
          to={link.to}
          className="px-3 py-2 bg-amber-600 text-slate-200 rounded-md"
        >
          {link.label}
        </Link>
      ))}
    </nav>
  );
};
