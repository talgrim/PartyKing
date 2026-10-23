import { ReactNode, useState } from 'react';
import { SessionDto } from '@/service/endpoints/sessions/types';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';

type Props = {
  session: SessionDto;
  /** The visitor is logged in as this session's host. */
  isHost: boolean;
};

/** Session name and code, plus either who the host is or the login that makes the visitor the host. */
export const SessionHeader = ({ session, isHost }: Props): ReactNode => {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loginAsHost = async () => {
    setBusy(true);
    try {
      window.location.href = await sessionsApi.createHostLoginUrl(session.id);
    } catch (e) {
      setError((e as { message?: string })?.message ?? 'Could not start the login');
      setBusy(false);
    }
  };

  return (
    <div className="px-2 pt-4">
      <h1 className="text-2xl text-slate-200">{session.name}</h1>
      <div className="text-sm text-slate-500">
        Code <span className="font-mono text-amber-500">{session.code}</span>
      </div>

      <div className="mt-2">
        {session.hasHost ? (
          <div className="flex items-center gap-3">
            <span>Current Host: {session.hostName}</span>
            {/* The host whose login expired gets back in here; anyone else is turned away after Spotify. */}
            {!isHost && (
              <button
                onClick={loginAsHost}
                disabled={busy}
                className="text-sm text-amber-500 underline cursor-pointer disabled:opacity-50"
              >
                I'm the host
              </button>
            )}
          </div>
        ) : (
          <button
            onClick={loginAsHost}
            disabled={busy}
            className="px-3 py-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50"
          >
            Log in as host with Spotify
          </button>
        )}
        {error && <p className="mt-2 text-sm text-amber-500">{error}</p>}
      </div>
    </div>
  );
};
