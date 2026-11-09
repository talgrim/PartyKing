import { FormEvent, ReactNode, useId, useState } from 'react';
import CloseIcon from '@mui/icons-material/Close';
import PlaylistAddIcon from '@mui/icons-material/PlaylistAdd';
import { Dialog, DialogContent, DialogTitle } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useAccount } from '@/service/endpoints/account/useAccount';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { playlistsApi } from '@/service/endpoints/playlists/playlistsApi';
import { PlaylistSection, SavedPlaylistDto } from '@/service/endpoints/playlists/types';
import { usePlaylistPreview } from '@/service/endpoints/playlists/usePlaylistPreview';
import { PLAYLIST_SECTIONS } from './playlistSections';
import { settingsButtonClass, settingsInputClass, settingsLabelClass, settingsMessageClass } from './settingsStyles';

/** Mirrors SavedPlaylist.MaxNameLength on the server. */
const MAX_NAME_LENGTH = 60;

type Props = {
  sessionId: string;
  /** The name the dialog starts with. */
  sessionName: string;
};

const initialSelection = () =>
  new Set(PLAYLIST_SECTIONS.filter((x) => x.defaultChecked).map((x) => x.section));

/**
 * Button with a dialog that saves chosen parts of the session's queue (played, playing now, up next, from the
 * fallback playlist) as a playlist of the signed-in account. Meant for signed-in accounts only; the API refuses
 * everyone else.
 */
export const SaveQueueAsPlaylist = ({ sessionId, sessionName }: Props): ReactNode => {
  const { t } = useTranslation();
  const { signedOut } = useAccount();
  const nameId = useId();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [checked, setChecked] = useState(initialSelection);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<SavedPlaylistDto | null>(null);
  const { counts, error: previewError, isLoading } = usePlaylistPreview(sessionId, open);

  const openDialog = () => {
    setName(sessionName.slice(0, MAX_NAME_LENGTH));
    setChecked(initialSelection());
    setError(null);
    setSaved(null);
    setOpen(true);
  };

  // A section with no songs cannot be chosen; until the counts arrive every section can
  const isEmpty = (section: PlaylistSection) => counts?.get(section) === 0;
  const selected = PLAYLIST_SECTIONS.filter((x) => checked.has(x.section) && !isEmpty(x.section)).map((x) => x.section);
  const canSave = !busy && name.trim().length > 0 && selected.length > 0;

  const toggle = (section: PlaylistSection) =>
    setChecked((current) => {
      const next = new Set(current);
      if (!next.delete(section)) next.add(section);
      return next;
    });

  const handleFailure = (failure: { status?: number; message?: string }) => {
    if (failure?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
      signedOut();
      setError(t('savePlaylist.signInAgain'));
      return;
    }
    setError(failure?.message || t('common.somethingWentWrong'));
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      setSaved(await playlistsApi.saveQueue(sessionId, name.trim(), selected));
    } catch (failure) {
      handleFailure(failure as { status?: number; message?: string });
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <button
        onClick={openDialog}
        aria-label={t('savePlaylist.title')}
        title={t('savePlaylist.title')}
        className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
      >
        <PlaylistAddIcon />
      </button>

      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        aria-labelledby="save-playlist-title"
        fullWidth
        maxWidth="xs"
        // MUI's emotion styles are unlayered and beat Tailwind's layered utilities, hence the important modifier
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200! rounded-xl!' } }}
      >
        <DialogTitle id="save-playlist-title" className="flex! items-center justify-between text-xl! font-semibold!">
          {t('savePlaylist.title')}
          <button
            onClick={() => setOpen(false)}
            aria-label={t('common.close')}
            title={t('common.close')}
            className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
          >
            <CloseIcon />
          </button>
        </DialogTitle>
        <DialogContent>
          {saved ? (
            <div className="space-y-4">
              <p role="status" className={settingsMessageClass}>
                {t('savePlaylist.saved', { count: saved.songCount, name: saved.name })}
              </p>
              <button type="button" onClick={() => setOpen(false)} className={settingsButtonClass}>
                {t('common.close')}
              </button>
            </div>
          ) : (
            <form className="space-y-4" onSubmit={(e) => void submit(e)}>
              <div>
                <label htmlFor={nameId} className={settingsLabelClass}>
                  {t('savePlaylist.name')}
                </label>
                <input
                  id={nameId}
                  className={settingsInputClass}
                  value={name}
                  maxLength={MAX_NAME_LENGTH}
                  onChange={(e) => setName(e.target.value)}
                />
              </div>

              <fieldset className="space-y-2">
                <legend className={settingsLabelClass}>{t('savePlaylist.songs')}</legend>
                {PLAYLIST_SECTIONS.map(({ section, label }) => {
                  const empty = isEmpty(section);
                  return (
                    <label
                      key={section}
                      className={`flex items-center gap-3 text-sm ${empty ? 'text-slate-500' : 'text-slate-300'}`}
                    >
                      <input
                        type="checkbox"
                        className="size-4 accent-amber-600"
                        checked={checked.has(section) && !empty}
                        disabled={empty}
                        onChange={() => toggle(section)}
                      />
                      <span className="flex-1">{label()}</span>
                      <span className="text-slate-400" aria-label={t('savePlaylist.songCount', { count: counts?.get(section) ?? 0 })}>
                        {counts ? counts.get(section) : isLoading ? '…' : ''}
                      </span>
                    </label>
                  );
                })}
                <p className="text-xs text-slate-400">{t('savePlaylist.duplicatesHint')}</p>
              </fieldset>

              {previewError && previewError.status !== HTTP_STATUS_CODE.UNAUTHORIZED && (
                <p role="alert" className={settingsMessageClass}>
                  {previewError.message || t('savePlaylist.countsFailed')}
                </p>
              )}
              {error && (
                <p role="alert" className={settingsMessageClass}>
                  {error}
                </p>
              )}

              <button type="submit" disabled={!canSave} className={settingsButtonClass}>
                {t('savePlaylist.save')}
              </button>
            </form>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
};
