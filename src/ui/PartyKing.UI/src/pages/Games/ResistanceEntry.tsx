import { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { sessionResistancePath } from '@/router/constants';

/** Link to the Resistance board. */
export const ResistanceEntry = ({ sessionId }: { sessionId: string }): ReactNode => (
  <Link
    to={sessionResistancePath(sessionId)}
    className="inline-block mt-2 px-2 py-1 bg-slate-800 rounded-md cursor-pointer"
  >
    Open
  </Link>
);
