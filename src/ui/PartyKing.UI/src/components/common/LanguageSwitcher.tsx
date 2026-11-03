import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { setLanguage } from '@/i18n';
import { isLanguage, LANGUAGE_NAMES, SUPPORTED_LANGUAGES } from '@/i18n/languages';

/**
 * The EN | PL toggle in the upper-left corner of every page. It is fixed and sits below the full-screen overlays
 * (z-50: the maximized view, the game dialogs), so those cover it instead of colliding with their own buttons.
 */
export const LanguageSwitcher = (): ReactNode => {
  const { t, i18n } = useTranslation();
  const current = i18n.resolvedLanguage;

  return (
    <div role="group" aria-label={t('language.label')} className="fixed top-1 left-1 z-40 flex overflow-hidden rounded-md bg-slate-900/80 text-xs">
      {SUPPORTED_LANGUAGES.map((language) => (
        <button
          key={language}
          type="button"
          lang={language}
          aria-label={LANGUAGE_NAMES[language]}
          aria-pressed={current === language}
          onClick={() => isLanguage(language) && void setLanguage(language)}
          className={`px-2 py-1 font-semibold uppercase cursor-pointer ${
            current === language ? 'bg-slate-200 text-slate-900' : 'text-slate-300 hover:bg-slate-700'
          }`}
        >
          {language}
        </button>
      ))}
    </div>
  );
};
