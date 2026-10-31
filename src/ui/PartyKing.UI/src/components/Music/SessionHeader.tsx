import { ReactNode, useState } from 'react';
import { SessionDto } from '@/service/endpoints/sessions/types';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { JoinQrCode } from './JoinQrCode';
import { OpenGamesMenu } from './OpenGamesMenu';
import { SessionSettings } from './SessionSettings';
import { useTranslation } from 'react-i18next';

type Props = {
  session: SessionDto;
  /** The visitor is logged in as this session's host. */
  isHost: boolean;
  /** The visitor is logged in as an admin. */
  isAdmin?: boolean;
  /** Called after the settings changed something the page shows, such as the session name. */
  onSessionChanged?: () => void;
};

/**
 * Card with the session name (the open games menu for everyone, the QR code and settings icons for the host and admins), its code, and either who the host
 * is or the login that makes the visitor the host.
 */
export const SessionHeader = ({ session, isHost, isAdmin = false, onSessionChanged }: Props): ReactNode => {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loginAsHost = async () => {
    setBusy(true);
    try {
      window.location.href = await sessionsApi.createHostLoginUrl(session.id);
    } catch (e) {
      setError((e as { message?: string })?.message ?? t('sessionHeader.loginFailed'));
      setBusy(false);
    }
  };

  return (
    <div className="mx-2 mt-4 p-4 bg-slate-900 rounded-lg">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold text-slate-100 break-words">{session.name}</h1>
          <div className="mt-1 text-sm text-slate-500">
            {t('sessionHeader.code')}{' '}
            <span className="px-2 py-0.5 font-mono text-amber-500 bg-slate-800 rounded-md">{session.code}</span>
          </div>
        </div>
        <div className="flex items-center gap-1 shrink-0">
          <OpenGamesMenu sessionId={session.id} canManage={isHost || isAdmin} />
          {(isHost || isAdmin) && (
            <>
              <JoinQrCode sessionId={session.id} />
              <SessionSettings session={session} onChanged={() => onSessionChanged?.()} />
            </>
          )}
        </div>
      </div>

      <div className="mt-3 pt-3 border-t border-slate-800">
        {session.hasHost ? (
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <span>{t('sessionHeader.currentHost', { name: session.hostName })}</span>
            {/* The host whose login expired gets back in here; anyone else is turned away after Spotify. */}
            {!isHost && (
              <button
                onClick={loginAsHost}
                disabled={busy}
                className="text-sm text-amber-500 underline cursor-pointer disabled:opacity-50"
              >
                {t('sessionHeader.imTheHost')}
              </button>
            )}
          </div>
        ) : (
          <button
            onClick={loginAsHost}
            disabled={busy}
            className="px-3 py-2 bg-amber-600 text-slate-100 rounded-md cursor-pointer hover:bg-amber-500 disabled:opacity-50"
          >
            {t('sessionHeader.loginAsHost')}
          </button>
        )}
        {error && <p className="mt-2 text-sm text-amber-500">{error}</p>}
      </div>
    </div>
  );
};
