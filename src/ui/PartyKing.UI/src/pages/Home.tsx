import { ReactNode, useState } from 'react';
import { Link } from 'react-router-dom';
import useSWR from 'swr';
import { sessionPath } from '@/router/constants';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';

/** Landing page: every session, with a search box that filters by code (or name). */
export const Home = (): ReactNode => {
  const [search, setSearch] = useState('');

  const { data: sessions, error } = useSWR('sessions', sessionsApi.getSessions, {
    refreshInterval: 10000,
  });

  const term = search.trim().toLowerCase();
  const visible = (sessions ?? []).filter(
    (session) => !term || session.code.toLowerCase().includes(term) || session.name.toLowerCase().includes(term),
  );

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="max-w-xl mx-auto">
        <h1 className="px-2 pt-4 text-2xl text-slate-200">Join a party</h1>

        <div className="mt-4 mb-2 px-2">
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full px-2 py-2 border-1 border-gray-700 rounded-sm focus:outline-none"
            placeholder="Search by session code"
            aria-label="Search sessions by code"
          />
        </div>

        {error && <p className="px-2 text-amber-500">Could not load the sessions</p>}
        {!error && !sessions && <p className="px-2">Loading sessions…</p>}
        {sessions && sessions.length === 0 && <p className="px-2">There are no sessions yet.</p>}
        {sessions && sessions.length > 0 && visible.length === 0 && (
          <p className="px-2">No session matches “{search.trim()}”.</p>
        )}

        <ul className="px-2 pb-4 space-y-2">
          {visible.map((session) => (
            <li key={session.id}>
              <Link
                to={sessionPath(session.id)}
                className="flex items-center justify-between gap-2 p-3 bg-slate-900 rounded-md hover:bg-slate-700"
              >
                <div className="min-w-0">
                  <div className="truncate text-lg text-slate-200">{session.name}</div>
                  <div className="truncate text-sm text-slate-500">
                    {session.hasHost ? `Host: ${session.hostName}` : 'No host yet'}
                  </div>
                </div>
                <span className="font-mono text-amber-500">
                  <span aria-label="Password protected" role="img">
                    🔒
                  </span>{' '}
                  {session.code}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
};
