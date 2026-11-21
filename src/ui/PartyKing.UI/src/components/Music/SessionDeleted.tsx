import { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ROUTE_PATHS } from '@/router/constants';

/** Shown instead of the password form when a session that was open has been deleted, by the host, an admin or the deletion a guest asked for. */
export const SessionDeleted = (): ReactNode => {
  const { t } = useTranslation();

  return (
    <div role="status" className="mx-2 mt-12 p-4 bg-slate-900 rounded-lg">
      <h1 className="text-2xl font-semibold text-slate-100">{t('sessionDeletion.deleted.title')}</h1>
      <p className="mt-2">{t('sessionDeletion.deleted.text')}</p>
      <p className="mt-4">
        <Link to={ROUTE_PATHS.HOME} className="underline">
          {t('common.backToSessions')}
        </Link>
      </p>
    </div>
  );
};
