import { FormEvent, ReactNode, useEffect, useState } from 'react';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { adminApi } from '@/service/endpoints/admin/adminApi';
import { secondsToTimeSpan, timeSpanToSeconds } from '@/service/endpoints/sessions/slideTime';

type Props = {
  sessionId: string;
  /** Called when the admin's login is gone. */
  onLoggedOut: () => void;
};

const MIN_SLIDE_SECONDS = 1;
const MAX_SLIDE_SECONDS = 3600;

const inputClass = 'w-24 px-2 py-1 border-1 border-gray-700 rounded-sm focus:outline-none';

/** Slide time and repeat options of one session's slideshow. */
export const SlideshowSettingsForm = ({ sessionId, onLoggedOut }: Props): ReactNode => {
  const [slideSeconds, setSlideSeconds] = useState<number | null>(null);
  const [autoRepeat, setAutoRepeat] = useState(false);
  const [startFromBeginning, setStartFromBeginning] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const fail = (error: unknown) => {
    if ((error as { status?: number })?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
      onLoggedOut();
      return;
    }
    setMessage((error as { message?: string })?.message || 'Something went wrong');
  };

  useEffect(() => {
    let cancelled = false;

    adminApi
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  if (slideSeconds === null) {
    return <p className="mt-2 text-sm">{message ?? 'Loading the settings…'}</p>;
  }

  const valid = slideSeconds >= MIN_SLIDE_SECONDS && slideSeconds <= MAX_SLIDE_SECONDS;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setMessage(null);
    try {
      await adminApi.updateSlideshowSettings(sessionId, {
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
    <form className="mt-2 space-y-2 text-sm" onSubmit={submit}>
      <label className="flex items-center gap-2">
        Seconds per photo
        <input
          className={inputClass}
          type="number"
          min={MIN_SLIDE_SECONDS}
          max={MAX_SLIDE_SECONDS}
          value={slideSeconds}
          onChange={(e) => setSlideSeconds(Number(e.target.value))}
        />
      </label>
      <label className="flex items-center gap-2">
        <input type="checkbox" checked={autoRepeat} onChange={(e) => setAutoRepeat(e.target.checked)} />
        Start over after the last photo
      </label>
      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          checked={startFromBeginning}
          onChange={(e) => setStartFromBeginning(e.target.checked)}
        />
        Show photos that were already presented again
      </label>
      <button
        type="submit"
        disabled={busy || !valid}
        className="px-2 py-1 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50"
      >
        Save
      </button>
      {message && <p className="text-amber-500">{message}</p>}
    </form>
  );
};
