import { ReactNode, useEffect, useState } from 'react';
import CheckIcon from '@mui/icons-material/Check';
import { useTranslation } from 'react-i18next';
import { AccountDialog } from '@/components/Account/AccountDialog';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { useAccount } from '@/service/endpoints/account/useAccount';
import { useSavedPlaylists } from '@/service/endpoints/playlists/useSavedPlaylists';
import { settingsButtonClass, settingsLabelClass, settingsMessageClass } from './settingsStyles';

type Props = {
  /** The saved playlist that is the session's fallback now, if any. */
  inUseId: string | null;
  selectedId: string | null;
  onSelect: (id: string) => void;
};

/**
 * The playlists the signed-in account saved, to pick the fallback playlist from. The account owns them, so a host or
 * admin without one is asked to log in instead.
 */
export const SavedPlaylistPicker = ({ inUseId, selectedId, onSelect }: Props): ReactNode => {
  const { t } = useTranslation();
  const { account, isLoading: accountLoading, signedIn, signedOut } = useAccount();
  const { playlists, error } = useSavedPlaylists(account !== null);
  const [accountDialogOpen, setAccountDialogOpen] = useState(false);

  // A login that expired is the same as no login
  useEffect(() => {
    if (error?.status === HTTP_STATUS_CODE.UNAUTHORIZED) signedOut();
  }, [error, signedOut]);

  if (!account) {
    return accountLoading ? null : (
      <div className="space-y-2">
        <p className={settingsLabelClass}>{t('myPlaylists.choose')}</p>
        <p className="text-sm text-slate-400">{t('myPlaylists.signInHint')}</p>
        <button type="button" className={settingsButtonClass} onClick={() => setAccountDialogOpen(true)}>
          {t('account.logIn')}
        </button>
        <AccountDialog
          open={accountDialogOpen}
          onClose={() => setAccountDialogOpen(false)}
          account={null}
          signedIn={signedIn}
          signedOut={signedOut}
        />
      </div>
    );
  }

  return (
    <div>
      <p className={settingsLabelClass}>{t('myPlaylists.choose')}</p>
      {error && error.status !== HTTP_STATUS_CODE.UNAUTHORIZED && (
        <p role="alert" className={settingsMessageClass}>
          {error.message || t('myPlaylists.loadFailed')}
        </p>
      )}
      {playlists?.length === 0 && <p className="text-sm text-slate-400">{t('myPlaylists.empty')}</p>}
      {playlists && playlists.length > 0 && (
        <ul aria-label={t('myPlaylists.choose')} className="max-h-56 overflow-y-auto space-y-1 pr-1">
          {playlists.map((playlist) => {
            const isSelected = selectedId === playlist.id;
            return (
              <li key={playlist.id}>
                <button
                  type="button"
                  aria-pressed={isSelected}
                  onClick={() => onSelect(playlist.id)}
                  className={`flex items-center w-full gap-3 p-2 text-left rounded-md cursor-pointer border ${
                    isSelected
                      ? 'bg-amber-600/20 border-amber-600'
                      : 'bg-slate-800 border-transparent hover:bg-slate-700'
                  }`}
                >
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-slate-100">{playlist.name}</span>
                    <span className="block text-xs text-slate-400">
                      {t('savePlaylist.songCount', { count: playlist.songCount })}
                      {inUseId === playlist.id && ` · ${t('myPlaylists.inUse')}`}
                    </span>
                  </span>
                  {isSelected && <CheckIcon fontSize="small" className="shrink-0 text-amber-500" />}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
};
