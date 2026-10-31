import { FormEvent, ReactNode, useEffect, useRef, useState } from 'react';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { secondsToTimeSpan, timeSpanToSeconds } from '@/service/endpoints/sessions/slideTime';
import { settingsButtonClass, settingsInputClass, settingsMessageClass } from './settingsStyles';
import { useTranslation } from 'react-i18next';
import { i18n } from '@/i18n';

type Props = {
  sessionId: string;
};

const MIN_SLIDE_SECONDS = 1;
const MAX_SLIDE_SECONDS = 3600;

const errorText = (error: unknown): string =>
  (error as { message?: string })?.message || i18n.t('common.somethingWentWrong');

/** Slide time and repeat options of one session's slideshow. */
export const SlideshowSettingsForm = ({ sessionId }: Props): ReactNode => {
  const { t } = useTranslation();
  const [slideSeconds, setSlideSeconds] = useState<number | null>(null);
  const [autoRepeat, setAutoRepeat] = useState(false);
  const [startFromBeginning, setStartFromBeginning] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const secondsInput = useRef<HTMLInputElement>(null);

  const fail = (error: unknown) => setMessage(errorText(error));

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
      .catch((error) => !cancelled && setMessage(errorText(error)));

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
    return <p className="text-sm text-slate-400">{message ?? t('slideshowSettings.loading')}</p>;
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
      setMessage(t('slideshowSettings.saved'));
    } catch (error) {
      fail(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="space-y-3" onSubmit={submit}>
      <label className="flex items-center justify-between gap-3 text-sm text-slate-300">
        {t('slideshowSettings.secondsPerPhoto')}
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
        {t('slideshowSettings.repeat')}
      </label>
      <label className="flex items-center gap-3 text-sm text-slate-300">
        <input
          type="checkbox"
          className="size-4 accent-amber-600"
          checked={startFromBeginning}
          onChange={(e) => setStartFromBeginning(e.target.checked)}
        />
        {t('slideshowSettings.showAgain')}
      </label>
      <div className="flex items-center gap-3">
        <button type="submit" disabled={busy || !valid} className={settingsButtonClass}>
          {t('common.save')}
        </button>
        {message && <p className={settingsMessageClass}>{message}</p>}
      </div>
    </form>
  );
};
