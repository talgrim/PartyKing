import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { privacyUrl } from '@/service/sites';
import { useHidesBottomBars } from '@/components/Layout/useHidesBottomBars';

/** The link to the privacy policy, at the bottom of every page that is not a full-screen game board. */
export const PrivacyFooter = (): ReactNode => {
  const { t, i18n } = useTranslation();
  const fullScreen = useHidesBottomBars();

  if (fullScreen) return null;

  return (
    <footer className="py-3 text-center text-xs text-slate-500">
      <a
        href={privacyUrl(i18n.language)}
        target="_blank"
        rel="noopener"
        className="underline hover:text-slate-300 focus-visible:outline-2 focus-visible:outline-amber-500"
      >
        {t('privacy.link')}
      </a>
    </footer>
  );
};
