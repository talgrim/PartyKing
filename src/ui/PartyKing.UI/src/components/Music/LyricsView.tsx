import ZoomInIcon from '@mui/icons-material/ZoomIn';
import ZoomOutIcon from '@mui/icons-material/ZoomOut';
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

const OFFSET_STORAGE_KEY = 'partyking.lyricsOffsetMs';
const FONT_SCALE_STORAGE_KEY = 'partyking.lyricsFontScale';
// Lines change every few seconds, so a few checks a second are plenty
const TICK_MS = 250;

type Props = {
  lyrics: LyricsState;
  nowPlaying: NowPlayingDto | undefined;
};

const offsetButtonClass =
  'rounded-md border border-slate-500 bg-slate-900 px-2 py-1 text-sm text-slate-200 hover:bg-slate-700 cursor-pointer disabled:cursor-default disabled:opacity-40 disabled:hover:bg-slate-900';

/**
 * The lyrics of the song playing, with the line being sung highlighted and scrolled to the middle. Where the song is
 * comes from now-playing: the server's position as of the last response, counted on locally while the song plays.
 * The host can nudge the timing when the sound reaches them late or early.
 */
export const LyricsView = ({ lyrics, nowPlaying }: Props): ReactNode => (
  <div className="relative flex h-full w-full flex-col bg-black text-slate-200">
    {lyrics.status === 'found' ? (
      <SyncedLyrics lines={lyrics.lines} nowPlaying={nowPlaying} />
    ) : (
      <p className="m-auto px-6 text-center text-xl text-slate-400">{emptyMessage(lyrics.status)}</p>
    )}
  </div>
);

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
}: {
  lines: Extract<LyricsState, { status: 'found' }>['lines'];
  nowPlaying: NowPlayingDto | undefined;
}): ReactNode => {
  const [offsetMs, setOffsetMs] = usePersistedNumber(OFFSET_STORAGE_KEY, 0, -MAX_OFFSET_MS, MAX_OFFSET_MS);
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

  useEffect(() => {
    const update = () => {
      const { positionMs: base, at } = anchor.current;
      setPositionMs(base + (running ? performance.now() - at : 0) + offsetRef.current);
    };
    update();
    const timer = setInterval(update, TICK_MS);
    return () => clearInterval(timer);
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
      </div>
    </>
  );
};
