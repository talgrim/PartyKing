import { ReactNode, useState } from 'react';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
  /** Every rule suggested so far, sent along so the model doesn't repeat them. */
  suggestedRules: string[];
  onSuggested: (rules: string[]) => void;
};

/** Asks the language model for new game rules, for the ace that was just flipped. */
export const RingoRuleSuggestions = ({ sessionId, suggestedRules, onSuggested }: Props): ReactNode => {
  const { t } = useTranslation();
  const [latest, setLatest] = useState<string[]>([]);
  const [fromFallback, setFromFallback] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const suggest = async () => {
    setBusy(true);
    setMessage(null);
    try {
      const result = await gamesApi.getRingoRuleSuggestions(sessionId, suggestedRules);
      setLatest(result.suggestions);
      setFromFallback(result.fromFallback);
      onSuggested(result.suggestions);
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
    <div className="max-w-xl max-h-48 overflow-y-auto">
      <button
        type="button"
        className="px-3 py-1 text-slate-100 bg-slate-800 rounded-md cursor-pointer disabled:opacity-50"
        disabled={busy}
        onClick={suggest}
      >
        {t('ringo.suggestRules')}
      </button>
      {message && <p className="pt-2 text-sm text-amber-500">{message}</p>}
      {fromFallback && (
        <p className="pt-2 text-sm text-amber-500">{t('ringo.suggestFallback')}</p>
      )}
      <ul className="pt-2 space-y-1 list-disc list-inside text-slate-200">
        {latest.map((rule) => (
          <li key={rule}>{rule}</li>
        ))}
      </ul>
    </div>
  );
};
