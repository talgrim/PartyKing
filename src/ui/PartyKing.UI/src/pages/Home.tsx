import { ReactNode, useState } from 'react';
import { Link } from 'react-router-dom';
import useSWR from 'swr';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ROUTE_PATHS, sessionPath } from '@/router/constants';
import { useIsAdmin } from '@/service/endpoints/admin/useIsAdmin';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';

/** Landing page: every session, with a search box that filters by code (or name). */
export const Home = (): ReactNode => {
  const [search, setSearch] = useState('');
  const isAdmin = useIsAdmin();

  const { data: sessions, error } = useSWR('sessions', sessionsApi.getSessions, {
    refreshInterval: 10000,
  });

  const term = search.trim().toLowerCase();
  const visible = (sessions ?? []).filter(
    (session) => !term || session.code.toLowerCase().includes(term) || session.name.toLowerCase().includes(term),
  );

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="w-full max-w-xl mx-auto">
        {isAdmin && <PageNavigation className="px-2 pt-4" links={[{ to: ROUTE_PATHS.ADMIN, label: 'Admin panel' }]} />}

        <div className="mx-2 mt-4 p-4 bg-slate-900 rounded-lg">
          <h1 className="text-2xl font-semibold text-slate-100">Join a party</h1>
          <div className="mt-1 text-sm text-slate-500">Pick a session below or search by its code</div>
        </div>

        <section className="mx-2 mt-4 p-3 bg-slate-900 rounded-lg">
          <h2 className="mb-2 text-xs font-semibold tracking-wider text-slate-400 uppercase">Find a session</h2>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-md placeholder:text-slate-500 focus:outline-none focus:border-amber-600"
            placeholder="Search by session code"
            aria-label="Search sessions by code"
          />
        </section>

        {error && <p className="px-2 pt-4 text-amber-500">Could not load the sessions</p>}
        {!error && !sessions && <p className="px-2 pt-4">Loading sessions…</p>}
        {sessions && sessions.length === 0 && <p className="px-2 pt-4">There are no sessions yet.</p>}
        {sessions && sessions.length > 0 && visible.length === 0 && (
          <p className="px-2 pt-4">No session matches “{search.trim()}”.</p>
        )}

        {visible.length > 0 && (
          <section className="mx-2 mt-4 mb-4 p-3 bg-slate-900 rounded-lg">
            <h2 className="mb-2 text-xs font-semibold tracking-wider text-amber-500 uppercase">Sessions</h2>
            <ul className="space-y-2">
              {visible.map((session) => (
                <li key={session.id}>
                  <Link
                    to={sessionPath(session.id)}
                    className="flex items-center justify-between gap-2 p-3 bg-slate-800 rounded-md hover:bg-slate-700"
                  >
                    <div className="min-w-0">
                      <div className="truncate text-lg font-semibold text-slate-100">{session.name}</div>
                      <div className="truncate text-sm text-slate-500">
                        {session.hasHost ? `Host: ${session.hostName}` : 'No host yet'}
                      </div>
                    </div>
                    <span className="shrink-0 px-2 py-0.5 font-mono text-amber-500 bg-slate-900 rounded-md">
                      <span aria-label="Password protected" role="img">
                        🔒
                      </span>{' '}
                      {session.code}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </div>
  );
};
