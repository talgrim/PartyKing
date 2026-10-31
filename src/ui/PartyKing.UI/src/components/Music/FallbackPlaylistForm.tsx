import { FormEvent, ReactNode, useEffect, useId, useState } from 'react';
import CheckIcon from '@mui/icons-material/Check';
import { CoverImage } from '@/components/Music/CoverImage';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { PlaylistDto, SessionDto } from '@/service/endpoints/sessions/types';
import {
  settingsButtonClass,
  settingsInputClass,
  settingsLabelClass,
  settingsMessageClass,
} from './settingsStyles';
import { useTranslation } from 'react-i18next';
import { i18n } from '@/i18n';

type Props = {
  session: SessionDto;
  /** Called after the playlist changed, so the session page picks up the new name. */
  onChanged: () => void;
};

/** The host's playlist that plays whenever the queue is empty. Only playlists the host owns are accepted. */
export const FallbackPlaylistForm = ({ session, onChanged }: Props): ReactNode => {
  const { t } = useTranslation();
  const inputId = useId();
  const [playlist, setPlaylist] = useState('');
  const [shuffle, setShuffle] = useState(session.fallbackShuffle ?? false);
  const [playlists, setPlaylists] = useState<PlaylistDto[]>([]);
  const [pickerMessage, setPickerMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  // The picker lists the host's own playlists. An admin who is not the host gets a 403 and just pastes a link.
  useEffect(() => {
    let cancelled = false;

    sessionsApi
      .getHostPlaylists(session.id)
      .then((owned) => !cancelled && setPlaylists(owned))
      .catch((error) => {
        if (cancelled || error?.status === HTTP_STATUS_CODE.FORBIDDEN) return;
        setPickerMessage(error?.message || i18n.t('fallback.loadFailed'));
      });

    return () => {
      cancelled = true;
    };
  }, [session.id]);

  const save = async (value: string | null, doneMessage: string) => {
    setBusy(true);
    setMessage(null);
    try {
      await sessionsApi.setFallbackPlaylist(session.id, value, shuffle);
      setPlaylist('');
      setMessage(doneMessage);
      onChanged();
    } catch (error) {
      setMessage((error as { message?: string })?.message || t('common.somethingWentWrong'));
    } finally {
      setBusy(false);
    }
  };

  const submit = (e: FormEvent) => {
    e.preventDefault();
    void save(playlist.trim(), t('fallback.saved'));
  };

  const selected = playlists.find((p) => p.uri === playlist.trim());
  // The session only stores the name, so the cover of the current playlist is found through the host's list
  const current = playlists.find((p) => p.name === session.fallbackPlaylistName);

  return (
    <form className="space-y-4" onSubmit={submit}>
      {session.fallbackPlaylistName ? (
        <div className="flex items-center gap-3 p-3 bg-slate-800 rounded-lg">
          <CoverImage imageUrl={current?.imageUrl ?? null} size="lg" />
          <div className="min-w-0 flex-1">
            <p className="text-xs tracking-wider text-slate-400 uppercase">{t('fallback.playsWhenEmpty')}</p>
            <p className="truncate font-semibold text-slate-100" title={session.fallbackPlaylistName}>
              {session.fallbackPlaylistName}
            </p>
          </div>
          <button
            type="button"
            disabled={busy}
            onClick={() => void save(null, t('fallback.removed'))}
            className="shrink-0 px-3 py-1.5 text-sm text-slate-200 border border-slate-600 rounded-md cursor-pointer hover:bg-slate-700 disabled:opacity-50 disabled:cursor-default"
          >
            {t('common.remove')}
          </button>
        </div>
      ) : (
        <p className="text-sm text-slate-400">
          {t('fallback.hint')}
        </p>
      )}

      {playlists.length > 0 && (
        <div>
          <p className={settingsLabelClass}>{t('fallback.yourPlaylists')}</p>
          <ul aria-label={t('fallback.yourPlaylists')} className="max-h-56 overflow-y-auto space-y-1 pr-1">
            {playlists.map((p) => {
              const isSelected = selected?.uri === p.uri;
              return (
                <li key={p.uri}>
                  <button
                    type="button"
                    aria-pressed={isSelected}
                    onClick={() => setPlaylist(p.uri)}
                    className={`flex items-center w-full gap-3 p-2 text-left rounded-md cursor-pointer border ${
                      isSelected
                        ? 'bg-amber-600/20 border-amber-600'
                        : 'bg-slate-800 border-transparent hover:bg-slate-700'
                    }`}
                  >
                    <CoverImage imageUrl={p.imageUrl} />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-slate-100">{p.name}</span>
                      <span className="block text-xs text-slate-400">
                        {t('fallback.tracks', { count: p.trackCount })}
                      </span>
                    </span>
                    {isSelected && <CheckIcon fontSize="small" className="shrink-0 text-amber-500" />}
                  </button>
                </li>
              );
            })}
          </ul>
        </div>
      )}
      {pickerMessage && <p className={settingsMessageClass}>{pickerMessage}</p>}

      <div>
        <label htmlFor={inputId} className={settingsLabelClass}>
          {playlists.length > 0 ? t('fallback.orPaste') : t('fallback.link')}
        </label>
        <input
          id={inputId}
          className={settingsInputClass}
          value={playlist}
          placeholder="https://open.spotify.com/playlist/…"
          onChange={(e) => setPlaylist(e.target.value)}
        />
      </div>

      <label className="flex items-center gap-3 text-sm text-slate-300">
        <input
          type="checkbox"
          className="size-4 accent-amber-600"
          checked={shuffle}
          onChange={(e) => setShuffle(e.target.checked)}
        />
        {t('fallback.shuffle')}
      </label>

      <div className="flex items-center gap-3">
        <button type="submit" disabled={busy || !playlist.trim()} className={settingsButtonClass}>
          {t('common.save')}
        </button>
        {message && <p className={settingsMessageClass}>{message}</p>}
      </div>
    </form>
  );
};
