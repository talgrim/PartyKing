import { ReactNode, useState } from 'react';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { GameSuggestionsDto } from '@/service/endpoints/games/types';
import { useTranslation } from 'react-i18next';

type Props = { sessionId: string };

/** Asks the language model for spicy "Never have I ever" statements, to read out to the guests. */
export const NeverHaveIEverSuggestions = ({ sessionId }: Props): ReactNode => {
  const { t } = useTranslation();
  const [result, setResult] = useState<GameSuggestionsDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const suggest = async () => {
    setBusy(true);
    setMessage(null);
    try {
      setResult(await gamesApi.getNeverHaveIEverSuggestions(sessionId));
    } catch (error) {
      setMessage(
        (error as { status?: number })?.status === HTTP_STATUS_CODE.TOO_MANY_REQUESTS
          ? t('common.tooManyRequests')
          : t('common.suggestionsFailed'),
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mt-2">
      <button
        type="button"
        className="px-2 py-1 bg-slate-800 rounded-md cursor-pointer disabled:opacity-50"
        disabled={busy}
        onClick={suggest}
      >
        {t('games.neverSuggest')}
      </button>
      {message && <p className="pt-2 text-sm text-amber-500">{message}</p>}
      {result && (
        <>
          {result.fromFallback && (
            <p className="pt-2 text-sm text-amber-500">{t('games.neverFallback')}</p>
          )}
          <ul className="pt-2 space-y-1 list-disc list-inside text-slate-200">
            {result.suggestions.map((statement) => (
              <li key={statement}>{statement}</li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
};
