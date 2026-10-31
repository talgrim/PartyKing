import type { InternalAxiosRequestConfig } from 'axios';
import { i18n } from '@/i18n';
import { DEFAULT_LANGUAGE, isLanguage } from '@/i18n/languages';

/**
 * Tells the API which language the UI is in, so the texts it generates (game names, suggestions) match.
 * Read at call time, so a switch applies to the next request.
 */
export const onRequestFulfilled = (config: InternalAxiosRequestConfig): InternalAxiosRequestConfig => {
  config.headers.set('Accept-Language', isLanguage(i18n.language) ? i18n.language : DEFAULT_LANGUAGE);

  return config;
};
