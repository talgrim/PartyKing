import { FormEvent, ReactNode, useEffect, useId, useState } from 'react';
import CheckIcon from '@mui/icons-material/Check';
import { CoverImage } from '@/components/Music/CoverImage';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { useAccount } from '@/service/endpoints/account/useAccount';
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
import { SavedPlaylistPicker } from './SavedPlaylistPicker';

type Props = {
  session: SessionDto;
  /** Called after the playlist changed, so the session page picks up the new name. */
  onChanged: () => void;
};

/**
 * The playlist that plays whenever the queue is empty: one of the host's Spotify playlists (only those the host owns
 * are accepted) or one the signed-in account saved. One choice, one Shuffle and one Save for both.
 */
export const FallbackPlaylistForm = ({ session, onChanged }: Props): ReactNode => {
  const { t } = useTranslation();
  const { signedOut } = useAccount();
  const inputId = useId();
  const [playlist, setPlaylist] = useState('');
  const [savedId, setSavedId] = useState<string | null>(null);
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

  // The login that owns the saved playlists may have expired since the list was loaded
  const setSaved = async (id: string) => {
    try {
      await sessionsApi.setFallbackSavedPlaylist(session.id, id, shuffle);
    } catch (error) {
      if ((error as { status?: number })?.status !== HTTP_STATUS_CODE.UNAUTHORIZED) throw error;

      signedOut();
      throw new Error(t('myPlaylists.signInAgain'));
    }
  };

  const save = async (call: () => Promise<void>, doneMessage: string) => {
    setBusy(true);
    setMessage(null);
    try {
      await call();
      setPlaylist('');
      setSavedId(null);
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
    void save(
      () => (savedId ? setSaved(savedId) : sessionsApi.setFallbackPlaylist(session.id, playlist.trim(), shuffle)),
      t('fallback.saved'),
    );
  };

  // Only one playlist is chosen at a time, whichever list or the link it came from
  const pickSpotify = (uri: string) => {
    setPlaylist(uri);
    setSavedId(null);
  };

  const pickSaved = (id: string) => {
    setSavedId(id);
    setPlaylist('');
  };

  const selected = playlists.find((p) => p.uri === playlist.trim());
  // The session only stores the name, so the cover of the current playlist is found through the host's list; a
  // saved playlist has no cover and is not on that list, even when a Spotify playlist happens to have its name
  const current = session.fallbackSavedPlaylistId
    ? undefined
    : playlists.find((p) => p.name === session.fallbackPlaylistName);

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
            onClick={() => void save(() => sessionsApi.setFallbackPlaylist(session.id, null, shuffle), t('fallback.removed'))}
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
                    onClick={() => pickSpotify(p.uri)}
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

      <SavedPlaylistPicker
        inUseId={session.fallbackSavedPlaylistId ?? null}
        selectedId={savedId}
        onSelect={pickSaved}
      />

      <div>
        <label htmlFor={inputId} className={settingsLabelClass}>
          {playlists.length > 0 ? t('fallback.orPaste') : t('fallback.link')}
        </label>
        <input
          id={inputId}
          className={settingsInputClass}
          value={playlist}
          placeholder="https://open.spotify.com/playlist/…"
          onChange={(e) => {
            setPlaylist(e.target.value);
            setSavedId(null);
          }}
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
        <button type="submit" disabled={busy || (!playlist.trim() && !savedId)} className={settingsButtonClass}>
          {t('common.save')}
        </button>
        {message && <p className={settingsMessageClass}>{message}</p>}
      </div>
    </form>
  );
};
