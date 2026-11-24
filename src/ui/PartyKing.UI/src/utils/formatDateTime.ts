/** An ISO instant as a long date and a short time in the visitor's time zone and the app's language, e.g. "28 November 2026 at 14:05". */
export const formatDateTime = (iso: string, language: string): string =>
  new Date(iso).toLocaleString(language, { dateStyle: 'long', timeStyle: 'short' });
