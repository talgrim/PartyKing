import { ReactNode, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { sessionPath } from '@/router/constants';
import { demoApi } from '@/service/endpoints/demo/demoApi';
import { MyDemoSessionDto } from '@/service/endpoints/demo/types';
import { useMyDemoSessions } from '@/service/endpoints/demo/useMyDemoSessions';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { CountdownLevel, formatCountdown } from './demoCountdown';
import { useDemoCountdown } from './useDemoCountdown';

const TIME_STYLES: Record<CountdownLevel, string> = {
  normal: 'text-slate-100',
  soon: 'text-amber-500',
  last: 'text-red-400',
};

type RowProps = {
  session: MyDemoSessionDto;
  serverTime: string;
  disabled: boolean;
  onOpen: (sessionId: string) => void;
};

/** One session with its own countdown; it is gone from the list once the time is up. */
const MyDemoSessionRow = ({ session, serverTime, disabled, onOpen }: RowProps): ReactNode => {
  const { t } = useTranslation();
  // One object per answer, so the countdown takes the server's clock once
  const demo = useMemo(() => ({ expiresAt: session.expiresAt, serverTime }), [session.expiresAt, serverTime]);
  const countdown = useDemoCountdown(demo);

  if (!countdown || countdown.ended) return null;

  return (
    <li className="flex items-center justify-between gap-2 p-3 bg-slate-800 rounded-md">
      <div className="min-w-0">
        <div className="truncate font-semibold text-slate-100">{session.name}</div>
        <div className={`font-mono text-sm ${TIME_STYLES[countdown.level]}`}>
          {t('demo.banner.endsIn', { time: formatCountdown(countdown.remainingMs) })}
        </div>
      </div>
      <button
        onClick={() => onOpen(session.sessionId)}
        disabled={disabled}
        className="shrink-0 px-3 py-2 text-slate-100 bg-amber-600 rounded-md cursor-pointer hover:bg-amber-500 disabled:opacity-50 disabled:cursor-default"
      >
        {t('demo.mine.open')}
      </button>
    </li>
  );
};

/**
 * The demo sessions this browser started and that have not ended, each with the time it has left, so a visitor who
 * comes back to the landing page can return to a party instead of starting another one. Nothing without any.
 */
export const MyDemoSessions = (): ReactNode => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data, mutate } = useMyDemoSessions();
  const [opening, setOpening] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The message stays when the list it is about just emptied
  if (!data || (data.sessions.length === 0 && !error)) return null;

  const open = async (sessionId: string) => {
    setOpening(true);
    setError(null);
    try {
      // Hosting an older session again replaces the cookies of the newer one, so the host's rights follow the click
      await demoApi.resume(sessionId);
      navigate(sessionPath(sessionId));
    } catch (e) {
      const status = (e as { status?: number } | undefined)?.status;
      setError(status === HTTP_STATUS_CODE.NOT_FOUND ? t('demo.mine.gone') : t('demo.mine.failed'));
      void mutate();
    } finally {
      setOpening(false);
    }
  };

  return (
    <section className="mx-2 mt-4 p-4 bg-slate-900 rounded-lg" aria-label={t('demo.mine.title')}>
      <h2 className="mb-2 text-xs font-semibold tracking-wider text-amber-500 uppercase">{t('demo.mine.title')}</h2>
      <ul className="space-y-2">
        {data.sessions.map((session) => (
          <MyDemoSessionRow
            key={session.sessionId}
            session={session}
            serverTime={data.serverTime}
            disabled={opening}
            onOpen={(sessionId) => void open(sessionId)}
          />
        ))}
      </ul>
      {error && <p className="mt-3 text-sm text-amber-500">{error}</p>}
    </section>
  );
};
