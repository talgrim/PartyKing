import { ReactNode, useEffect, useState } from 'react';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { settingsButtonClass } from './settingsStyles';
import { useTranslation } from 'react-i18next';
import { i18n } from '@/i18n';

type Props = {
  sessionId: string;
};

const errorText = (error: unknown): string =>
  (error as { message?: string })?.message || i18n.t('common.somethingWentWrong');

/** Downloads everything uploaded to the session as a zip. The link is disabled while there are no photos. */
export const PhotoDownload = ({ sessionId }: Props): ReactNode => {
  const { t } = useTranslation();
  const [count, setCount] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    sessionsApi
      .getPhotoCount(sessionId)
      .then((result) => !cancelled && setCount(result.count))
      .catch((e) => !cancelled && setError(errorText(e)));

    return () => {
      cancelled = true;
    };
  }, [sessionId]);

  if (count === null) {
    return <p className="text-sm text-slate-400">{error ?? t('photoDownload.loading')}</p>;
  }

  const hasPhotos = count > 0;

  return (
    <div className="space-y-3">
      <p className="text-sm text-slate-300">{hasPhotos ? t('photoDownload.count', { count }) : t('photoDownload.none')}</p>
      {/* Without an href the anchor is not a link, so it can't be followed; aria-disabled tells assistive tech why */}
      <a
        role="link"
        aria-disabled={!hasPhotos}
        href={hasPhotos ? sessionsApi.photoArchiveUrl(sessionId) : undefined}
        download
        className={`${settingsButtonClass} inline-block ${hasPhotos ? '' : 'opacity-50 cursor-default! hover:bg-amber-600!'}`}
      >
        {t('photoDownload.download')}
      </a>
      <p className="text-xs text-slate-500">{t('photoDownload.hint')}</p>
    </div>
  );
};
