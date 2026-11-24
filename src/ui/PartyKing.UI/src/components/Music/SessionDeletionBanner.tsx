import { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/formatDateTime';

type Props = {
  /** ISO instant at which the session is deleted. */
  deletionScheduledAt: string;
};

/**
 * Tells everyone in the session that it will be deleted, and when, because a guest asked for it. Only an admin can
 * call that off (in the admin panel), so there is nothing to press here.
 */
export const SessionDeletionBanner = ({ deletionScheduledAt }: Props): ReactNode => {
  const { t, i18n } = useTranslation();

  return (
    <div role="status" className="mx-2 mt-4 p-3 bg-slate-900 border border-amber-600 rounded-lg">
      <span className="text-xs font-semibold tracking-wider text-amber-500 uppercase">
        {t('sessionDeletion.banner.label')}
      </span>
      <p className="mt-1 text-slate-300">
        {t('sessionDeletion.banner.text', { date: formatDateTime(deletionScheduledAt, i18n.language) })}
      </p>
    </div>
  );
};
