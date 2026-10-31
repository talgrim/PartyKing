import { ReactNode } from 'react';
import { useAutoLyrics } from './useAutoLyrics';
import { useTranslation } from 'react-i18next';

/** Lets the slideshow drawer switch to the lyrics of each song that has some, and back to the slideshow when it has none. */
export const AutoLyricsForm = (): ReactNode => {
  const { t } = useTranslation();
  const [autoLyrics, setAutoLyrics] = useAutoLyrics();

  return (
    <label className="flex items-center gap-3 text-sm text-slate-300">
      <input
        type="checkbox"
        className="size-4 accent-amber-600"
        checked={autoLyrics}
        onChange={(e) => setAutoLyrics(e.target.checked)}
      />
      {t('autoLyrics.label')}
    </label>
  );
};
