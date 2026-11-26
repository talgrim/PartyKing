import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { privacyUrl } from '@/service/sites';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { useHidesBottomBars } from '@/components/Layout/useHidesBottomBars';

/**
 * Bump the version when the policy changes in a way people should read again: the notice then shows once more.
 * It is fixed to the bottom of the screen, not in the page flow, so it never moves the site bar or the full-screen pages.
 */
export const PRIVACY_NOTICE_KEY = 'partyking.privacyNotice.v1';

/**
 * Tells a visitor what PartyKing keeps, once. PartyKing sets only the cookies it needs to work and has no analytics, so
 * this is a notice and not a request for consent.
 */
export const PrivacyNotice = (): ReactNode => {
  const { t, i18n } = useTranslation();
  const [dismissed, setDismissed] = usePersistedFlag(PRIVACY_NOTICE_KEY, false);
  const fullScreen = useHidesBottomBars();

  if (dismissed || fullScreen) return null;

  return (
    <div role="region" aria-label={t('privacy.noticeLabel')} className="fixed inset-x-0 bottom-0 z-40 p-2">
      <div className="mx-auto flex max-w-xl flex-wrap items-center gap-x-4 gap-y-2 rounded-lg border border-slate-700 bg-slate-900 p-3 text-sm text-slate-300 shadow-lg">
        <p className="min-w-0 flex-1 basis-60">
          {t('privacy.notice')}{' '}
          <a
            href={privacyUrl(i18n.language, 'partyking')}
            target="_blank"
            rel="noopener"
            className="font-semibold text-amber-500 underline"
          >
            {t('privacy.link')}
          </a>
        </p>
        <button
          type="button"
          onClick={() => setDismissed(true)}
          className="rounded-md bg-amber-600 px-3 py-1.5 text-sm font-semibold text-slate-950 hover:bg-amber-500"
        >
          {t('privacy.dismiss')}
        </button>
      </div>
    </div>
  );
};
