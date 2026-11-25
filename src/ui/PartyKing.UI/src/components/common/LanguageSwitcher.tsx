import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { setLanguage } from '@/i18n';
import { isLanguage, LANGUAGE_NAMES, SUPPORTED_LANGUAGES } from '@/i18n/languages';
import { helpTarget } from '@/components/Help/helpSteps';

/** The EN | PL toggle of the site bar (styled by the kit's `site-bar__languages`). The choice is shared with the other sites. */
export const LanguageSwitcher = (): ReactNode => {
  const { t, i18n } = useTranslation();
  const current = i18n.resolvedLanguage;

  return (
    <div {...helpTarget('language')} role="group" aria-label={t('language.label')} className="site-bar__languages">
      {SUPPORTED_LANGUAGES.map((language) => (
        <button
          key={language}
          type="button"
          lang={language}
          aria-label={LANGUAGE_NAMES[language]}
          aria-pressed={current === language}
          onClick={() => isLanguage(language) && void setLanguage(language)}
        >
          {language.toUpperCase()}
        </button>
      ))}
    </div>
  );
};
