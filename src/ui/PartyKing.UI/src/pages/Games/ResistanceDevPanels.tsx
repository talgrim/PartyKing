import { ReactNode, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { sessionResistanceJoinPath, sessionResistancePath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { devPersonaFrameName } from '@/service/devPersona';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useTranslation } from 'react-i18next';

const MIN_PLAYERS = 5;
const MAX_PLAYERS = 10;
const DEFAULT_PLAYERS = 5;

/** Up to this many panels fit side by side in one row. */
const SINGLE_ROW_PLAYERS = 5;

const clampPlayers = (value: number): number =>
  Number.isFinite(value) ? Math.min(MAX_PLAYERS, Math.max(MIN_PLAYERS, Math.trunc(value))) : DEFAULT_PLAYERS;

const personaOf = (number: number): string => `dev-${number}`;
const nameOf = (number: number): string => `Player ${number}`;

const errorStatus = (error: unknown): number | undefined => (error as { status?: number } | undefined)?.status;
const errorReason = (error: unknown): string => (error as { message?: string } | undefined)?.message ?? '';

/**
 * Development only (the route exists in dev builds alone): every player of the session's Resistance game side by side in
 * one tab, 5 to 10 of them, to play a whole game alone. Each panel is a same-origin frame of the join page under its own
 * dev persona (`dev-1`, `dev-2`, ...), which gives it its own player cookie, API calls and hub connection, and shows
 * nothing but that player's view (see `isDevEmbedded`). This page keeps the admin identity of the tab it was opened
 * from, so it joins the personas, which needs the open game and a session unlocked in this browser, and starts the game.
 */
export const ResistanceDevPanels = (): ReactNode => {
  const { t } = useTranslation();
  const { sessionId = '' } = useParams();
  const [searchParams] = useSearchParams();
  const [count, setCount] = useState(() => clampPlayers(Number(searchParams.get('n') ?? DEFAULT_PLAYERS)));
  const [shown, setShown] = useState(0);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>();

  const joinFailure = (name: string, error: unknown): string => {
    switch (errorStatus(error)) {
      case HTTP_STATUS_CODE.UNAUTHORIZED:
        return t('resistance.dev.failedUnlock');
      case HTTP_STATUS_CODE.TOO_MANY_REQUESTS:
        return t('resistance.dev.failedRateLimit');
      default:
        return t('resistance.dev.failedJoin', { name, reason: errorReason(error) });
    }
  };

  const showPanels = async () => {
    setBusy(true);
    setMessage(undefined);
    setShown(0);
    try {
      for (let number = 1; number <= count; number++) {
        const persona = personaOf(number);
        try {
          const lobby = await gamesApi.getResistanceLobby(sessionId, persona);
          if (!lobby.joined) await gamesApi.joinResistance(sessionId, nameOf(number), persona);
        } catch (error) {
          setMessage(joinFailure(nameOf(number), error));
          return;
        }
      }
      setShown(count);
    } finally {
      setBusy(false);
    }
  };

  const startGame = async () => {
    setMessage(undefined);
    try {
      await gamesApi.startResistance(sessionId);
    } catch (error) {
      setMessage(t('resistance.dev.failedStart', { reason: errorReason(error) }));
    }
  };

  const rows = shown <= SINGLE_ROW_PLAYERS ? 1 : 2;
  const columns = Math.ceil(shown / rows);

  return (
    <div className="flex flex-col w-full h-screen gap-2 p-2 bg-slate-900 text-slate-300">
      <div className="flex flex-wrap items-center gap-3 pl-24 text-sm">
        <Link to={sessionResistancePath(sessionId)} className="underline">
          {t('resistance.dev.back')}
        </Link>
        <h1 className="text-slate-200">{t('resistance.dev.title')}</h1>
        <label className="flex items-center gap-2">
          {t('resistance.dev.playerCount')}
          <input
            type="number"
            min={MIN_PLAYERS}
            max={MAX_PLAYERS}
            value={count}
            onChange={(event) => setCount(clampPlayers(event.target.valueAsNumber))}
            className="w-16 px-2 py-1 rounded-md bg-slate-800 text-slate-200"
          />
        </label>
        <button
          type="button"
          disabled={busy}
          onClick={() => void showPanels()}
          className="px-3 py-1 rounded-md bg-slate-700 text-slate-200 hover:bg-slate-600 disabled:opacity-50 cursor-pointer"
        >
          {t('resistance.dev.show')}
        </button>
        <button
          type="button"
          disabled={busy || shown === 0}
          onClick={() => void startGame()}
          className="px-3 py-1 rounded-md bg-slate-700 text-slate-200 hover:bg-slate-600 disabled:opacity-50 cursor-pointer"
        >
          {t('resistance.dev.start')}
        </button>
        {shown > 0 && <span className="text-slate-400">{t('resistance.dev.joinedCount', { count: shown })}</span>}
        {message && (
          <span role="alert" className="text-amber-500">
            {message}
          </span>
        )}
      </div>

      {shown > 0 && (
        <div
          className="grid flex-1 min-h-0 gap-2"
          style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`, gridTemplateRows: `repeat(${rows}, minmax(0, 1fr))` }}
        >
          {Array.from({ length: shown }, (_, index) => index + 1).map((number) => (
            <section key={number} className="flex flex-col min-h-0 rounded-md overflow-hidden border border-slate-700">
              <h2 className="px-2 py-0.5 text-xs bg-slate-800 text-slate-300">{t('resistance.dev.panel', { number })}</h2>
              <iframe
                title={t('resistance.dev.panel', { number })}
                name={devPersonaFrameName(personaOf(number))}
                src={`${sessionResistanceJoinPath(sessionId)}?persona=${personaOf(number)}`}
                className="flex-1 min-h-0 w-full border-0"
              />
            </section>
          ))}
        </div>
      )}
    </div>
  );
};
