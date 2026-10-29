import CheckIcon from '@mui/icons-material/Check';
import ZoomInIcon from '@mui/icons-material/ZoomIn';
import ZoomOutIcon from '@mui/icons-material/ZoomOut';
import SyncIcon from '@mui/icons-material/Sync';
import { ReactNode, useEffect, useRef, useState } from 'react';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import { LyricsState } from '@/service/endpoints/lyrics/useLyrics';
import { usePersistedNumber } from '@/utils/usePersistedNumber';
import {
  FONT_SCALE_DEFAULT,
  FONT_SCALE_MAX,
  FONT_SCALE_MIN,
  FONT_SCALE_STEP,
  MAX_OFFSET_MS,
  OFFSET_STEP_MS,
  activeLineIndex,
  formatOffset,
} from './lyricsTiming';

const FONT_SCALE_STORAGE_KEY = 'partyking.lyricsFontScale';
// Lines change every few seconds, so a few checks a second are plenty
const TICK_MS = 250;

type Props = {
  lyrics: LyricsState;
  nowPlaying: NowPlayingDto | undefined;
  /** Refetches now-playing, called when the window gets back in front so the position is re-read from the server. */
  onResync?: () => void;
  /**
   * Saves the timing for everyone watching this song. Only the host and admins get it, and with it the save button.
   */
  onSaveOffset?: (offsetMs: number) => Promise<void>;
};

const offsetButtonClass =
  'rounded-md border border-slate-500 bg-slate-900 px-2 py-1 text-sm text-slate-200 hover:bg-slate-700 cursor-pointer disabled:cursor-default disabled:opacity-40 disabled:hover:bg-slate-900';

/**
 * The lyrics of the song playing, with the line being sung highlighted and scrolled to the middle. Where the song is
 * comes from now-playing: the server's position as of the last response, counted on locally while the song plays.
 * Anyone can nudge the timing when the sound reaches them late or early; the host and admins can save it for the
 * song, which is then the starting point of every viewer, now and the next time the song plays.
 */
export const LyricsView = ({ lyrics, nowPlaying, onResync, onSaveOffset }: Props): ReactNode => {
  // The saved offset of the song on display; 0 while there are no lyrics
  const savedOffsetMs = lyrics.status === 'found' ? lyrics.offsetMs : 0;
  const foundSongId = lyrics.status === 'found' ? lyrics.songId : undefined;

  // Kept here rather than in SyncedLyrics, which is unmounted while the next song's lyrics load. It starts at the
  // saved offset of each song, and a newly saved one (by this viewer or by the host) replaces whatever was nudged.
  const [offsetMs, setDraftMs] = useState(savedOffsetMs);
  useEffect(() => setDraftMs(savedOffsetMs), [foundSongId, savedOffsetMs]);
  const setOffsetMs = (value: number) => setDraftMs(Math.min(MAX_OFFSET_MS, Math.max(-MAX_OFFSET_MS, value)));

  return (
    <div className="relative flex h-full w-full flex-col bg-black text-slate-200">
      {lyrics.status === 'found' ? (
        <SyncedLyrics
          lines={lyrics.lines}
          nowPlaying={nowPlaying}
          onResync={onResync}
          offsetMs={offsetMs}
          setOffsetMs={setOffsetMs}
          savedOffsetMs={savedOffsetMs}
          onSaveOffset={onSaveOffset}
        />
      ) : (
        <p className="m-auto px-6 text-center text-xl text-slate-400">{emptyMessage(lyrics.status)}</p>
      )}
    </div>
  );
};

const emptyMessage = (status: LyricsState['status']) => {
  switch (status) {
    case 'loading':
      return 'Looking for the lyrics…';
    case 'none':
      return 'No synchronized lyrics for this song';
    default:
      return 'Nothing is playing';
  }
};

const SyncedLyrics = ({
  lines,
  nowPlaying,
  onResync,
  offsetMs,
  setOffsetMs,
  savedOffsetMs,
  onSaveOffset,
}: {
  lines: Extract<LyricsState, { status: 'found' }>['lines'];
  nowPlaying: NowPlayingDto | undefined;
  onResync?: () => void;
  offsetMs: number;
  setOffsetMs: (offsetMs: number) => void;
  savedOffsetMs: number;
  onSaveOffset?: (offsetMs: number) => Promise<void>;
}): ReactNode => {
  const [saving, setSaving] = useState(false);
  const saveOffset = async () => {
    if (!onSaveOffset) return;
    setSaving(true);
    try {
      await onSaveOffset(offsetMs);
    } catch {
      // The draft stays unsaved, so the button is still there to try again
    } finally {
      setSaving(false);
    }
  };
  const [fontScale, setFontScale] = usePersistedNumber(
    FONT_SCALE_STORAGE_KEY,
    FONT_SCALE_DEFAULT,
    FONT_SCALE_MIN,
    FONT_SCALE_MAX,
  );
  // Starts where the song is, so a remounted view (drawer <-> maximized) doesn't flash the first line
  const [positionMs, setPositionMs] = useState(() => (nowPlaying?.positionMs ?? 0) + offsetMs);
  const containerRef = useRef<HTMLDivElement>(null);
  const activeRef = useRef<HTMLParagraphElement>(null);
  const scrolledOnce = useRef(false);

  // Where the song was when the server last said so, and when that arrived here
  const anchor = useRef({ positionMs: 0, at: 0 });
  useEffect(() => {
    anchor.current = { positionMs: nowPlaying?.positionMs ?? 0, at: performance.now() };
  }, [nowPlaying]);

  const running = Boolean(nowPlaying?.song) && !nowPlaying?.paused && Boolean(nowPlaying?.playerConnected);
  const offsetRef = useRef(offsetMs);
  offsetRef.current = offsetMs;

  const onResyncRef = useRef(onResync);
  onResyncRef.current = onResync;

  const updateRef = useRef(() => {});
  const resyncNow = () => {
    updateRef.current();
    onResyncRef.current?.();
  };

  useEffect(() => {
    const update = () => {
      const { positionMs: base, at } = anchor.current;
      setPositionMs(base + (running ? performance.now() - at : 0) + offsetRef.current);
    };
    updateRef.current = update;
    update();
    const timer = setInterval(update, TICK_MS);

    // A hidden tab throttles the timer and may miss pushes (a seek, a pause), so on return catch up at once and ask
    // the server where the song is now
    const resync = () => {
      if (document.visibilityState === 'hidden') return;
      resyncNow();
    };
    window.addEventListener('focus', resync);
    document.addEventListener('visibilitychange', resync);
    return () => {
      clearInterval(timer);
      window.removeEventListener('focus', resync);
      document.removeEventListener('visibilitychange', resync);
    };
  }, [running, nowPlaying, offsetMs]);

  const active = activeLineIndex(lines, positionMs);

  // Keep the line being sung in the middle. Scrolling the container itself leaves the page where it is.
  useEffect(() => {
    const container = containerRef.current;
    const line = activeRef.current;
    if (!container || !line || typeof container.scrollTo !== 'function') return;

    // The first jump (a freshly mounted view) is instant, so it doesn't scroll down from the top
    container.scrollTo({
      top: line.offsetTop - container.clientHeight / 2 + line.clientHeight / 2,
      behavior: scrolledOnce.current ? 'smooth' : 'auto',
    });
    scrolledOnce.current = true;
  }, [active]);

  return (
    <>
      <div ref={containerRef} className="relative min-h-0 flex-1 overflow-y-auto px-6 py-[30%] text-center">
        {lines.map((line, index) => (
          <p
            key={`${line.timeMs}-${index}`}
            ref={index === active ? activeRef : undefined}
            aria-current={index === active ? 'true' : undefined}
            style={{ fontSize: `${((index === active ? 1.5 : 1.25) * fontScale) / 100}rem` }}
            className={
              index === active
                ? 'my-2 font-bold text-white transition-colors'
                : index < active
                  ? 'my-2 text-slate-600 transition-colors'
                  : 'my-2 text-slate-400 transition-colors'
            }
          >
            {line.text || '♪'}
          </p>
        ))}
      </div>
      <div
        role="group"
        aria-label="Lyrics size"
        className="absolute top-2 right-2 flex items-center gap-1 rounded-md bg-black/70 p-1"
      >
        <button
          type="button"
          className={offsetButtonClass}
          title="Smaller text"
          aria-label="Smaller lyrics"
          disabled={fontScale <= FONT_SCALE_MIN}
          onClick={() => setFontScale(fontScale - FONT_SCALE_STEP)}
        >
          <ZoomOutIcon fontSize="small" />
        </button>
        <button
          type="button"
          className={offsetButtonClass}
          title="Larger text"
          aria-label="Larger lyrics"
          disabled={fontScale >= FONT_SCALE_MAX}
          onClick={() => setFontScale(fontScale + FONT_SCALE_STEP)}
        >
          <ZoomInIcon fontSize="small" />
        </button>
      </div>
      <div
        role="group"
        aria-label="Lyrics timing"
        className="absolute right-2 bottom-2 flex items-center gap-1 rounded-md bg-black/70 p-1"
      >
        <button
          type="button"
          className={offsetButtonClass}
          title="Match the lyrics to the current position of the song"
          aria-label="Resync lyrics"
          onClick={resyncNow}
        >
          <SyncIcon fontSize="small" />
        </button>
        <button
          type="button"
          className={offsetButtonClass}
          title="Lyrics later"
          aria-label="Show lyrics later"
          onClick={() => setOffsetMs(offsetMs - OFFSET_STEP_MS)}
        >
          −
        </button>
        <button
          type="button"
          className={`${offsetButtonClass} min-w-16`}
          title="Reset the timing"
          aria-label={`Lyrics timing ${formatOffset(offsetMs)}, press to reset`}
          onClick={() => setOffsetMs(0)}
        >
          {formatOffset(offsetMs)}
        </button>
        <button
          type="button"
          className={offsetButtonClass}
          title="Lyrics earlier"
          aria-label="Show lyrics earlier"
          onClick={() => setOffsetMs(offsetMs + OFFSET_STEP_MS)}
        >
          +
        </button>
        {onSaveOffset && (
          <button
            type="button"
            className={offsetButtonClass}
            title="Save this timing for everyone"
            aria-label="Save lyrics timing"
            disabled={saving || offsetMs === savedOffsetMs}
            onClick={() => void saveOffset()}
          >
            <CheckIcon fontSize="small" />
          </button>
        )}
      </div>
    </>
  );
};
