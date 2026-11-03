import useSWR from 'swr';
import { useTranslation } from 'react-i18next';
import { gamesApi } from './gamesApi';

/**
 * The games of a session. The server answers only to the host and admins, so guests get an error with status 403.
 * Names and descriptions come in the UI language, so the language is part of the key and a switch refetches them.
 */
export const useGames = (sessionId: string) => {
  const { i18n } = useTranslation();

  return useSWR(['games', sessionId, i18n.language], () => gamesApi.getGames(sessionId), { shouldRetryOnError: false });
};
