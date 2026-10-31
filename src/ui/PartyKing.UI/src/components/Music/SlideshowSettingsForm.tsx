import { FormEvent, ReactNode, useEffect, useRef, useState } from 'react';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { secondsToTimeSpan, timeSpanToSeconds } from '@/service/endpoints/sessions/slideTime';
import { settingsButtonClass, settingsInputClass, settingsMessageClass } from './settingsStyles';

type Props = {
  sessionId: string;
};

const MIN_SLIDE_SECONDS = 1;
const MAX_SLIDE_SECONDS = 3600;

/** Slide time and repeat options of one session's slideshow. */
export const SlideshowSettingsForm = ({ sessionId }: Props): ReactNode => {
  const [slideSeconds, setSlideSeconds] = useState<number | null>(null);
  const [autoRepeat, setAutoRepeat] = useState(false);
  const [startFromBeginning, setStartFromBeginning] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const secondsInput = useRef<HTMLInputElement>(null);

  const fail = (error: unknown) => {
    setMessage((error as { message?: string })?.message || 'Something went wrong');
  };

  useEffect(() => {
    let cancelled = false;

    sessionsApi
      .getSlideshowSettings(sessionId)
      .then((settings) => {
        if (cancelled) return;
        setSlideSeconds(timeSpanToSeconds(settings.slideTime));
        setAutoRepeat(settings.autoRepeat);
        setStartFromBeginning(settings.startFromBeginning);
      })
      .catch((error) => !cancelled && fail(error));

    return () => {
      cancelled = true;
    };
  }, [sessionId]);

  // The wheel changes the seconds while the pointer is over the field, focused or not. React registers wheel
  // listeners as passive, so this one is attached by hand to be able to stop the dialog from scrolling.
  const loaded = slideSeconds !== null;
  useEffect(() => {
    const input = secondsInput.current;
    if (!input) return;

    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const step = e.deltaY < 0 ? 1 : -1;
      setSlideSeconds((seconds) =>
        Math.min(MAX_SLIDE_SECONDS, Math.max(MIN_SLIDE_SECONDS, (seconds ?? MIN_SLIDE_SECONDS) + step)),
      );
    };

    input.addEventListener('wheel', onWheel, { passive: false });
    return () => input.removeEventListener('wheel', onWheel);
  }, [loaded]);

  if (slideSeconds === null) {
    return <p className="text-sm text-slate-400">{message ?? 'Loading the settings…'}</p>;
  }

  const valid = slideSeconds >= MIN_SLIDE_SECONDS && slideSeconds <= MAX_SLIDE_SECONDS;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setMessage(null);
    try {
      await sessionsApi.updateSlideshowSettings(sessionId, {
        autoRepeat,
        startFromBeginning,
        slideTime: secondsToTimeSpan(slideSeconds),
      });
      setMessage('Slideshow settings saved');
    } catch (error) {
      fail(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="space-y-3" onSubmit={submit}>
      <label className="flex items-center justify-between gap-3 text-sm text-slate-300">
        Seconds per photo
        <input
          ref={secondsInput}
          className={`${settingsInputClass} w-24!`}
          type="number"
          min={MIN_SLIDE_SECONDS}
          max={MAX_SLIDE_SECONDS}
          value={slideSeconds}
          onChange={(e) => setSlideSeconds(Number(e.target.value))}
        />
      </label>
      <label className="flex items-center gap-3 text-sm text-slate-300">
        <input
          type="checkbox"
          className="size-4 accent-amber-600"
          checked={autoRepeat}
          onChange={(e) => setAutoRepeat(e.target.checked)}
        />
        Start over after the last photo
      </label>
      <label className="flex items-center gap-3 text-sm text-slate-300">
        <input
          type="checkbox"
          className="size-4 accent-amber-600"
          checked={startFromBeginning}
          onChange={(e) => setStartFromBeginning(e.target.checked)}
        />
        Show photos that were already presented again
      </label>
      <div className="flex items-center gap-3">
        <button type="submit" disabled={busy || !valid} className={settingsButtonClass}>
          Save
        </button>
        {message && <p className={settingsMessageClass}>{message}</p>}
      </div>
    </form>
  );
};
