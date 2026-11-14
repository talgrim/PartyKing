import CloseIcon from '@mui/icons-material/Close';
import Modal from '@mui/material/Modal';
import Popper from '@mui/material/Popper';
import type { Instance } from '@popperjs/core';
import { ReactNode, useEffect, useId, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSlideshowOpen } from '@/components/Music/useSlideshowOpen';
import { HelpStep } from './helpSteps';
import { TargetRect, useHelpTarget } from './useHelpTarget';

type Props = {
  steps: HelpStep[];
  onClose: () => void;
};

/** Room between the highlighted element and the edge of the hole around it. */
const PADDING = 6;
const CALLOUT_GAP = 14;

const toDomRect = (rect: TargetRect | null): DOMRect => {
  const { top, left, width, height } = rect ?? { top: 0, left: 0, width: 0, height: 0 };
  return { top, left, width, height, x: left, y: top, right: left + width, bottom: top + height, toJSON: () => ({}) };
};

const secondaryButtonClass = 'px-3 py-1 text-sm text-slate-300 border border-slate-600 rounded-md cursor-pointer hover:bg-slate-800';
const primaryButtonClass = 'px-3 py-1 text-sm font-semibold text-slate-100 bg-amber-600 rounded-md cursor-pointer hover:bg-amber-500';

/**
 * The guided tour: dims the page, cuts a hole around the real element of the current step and puts a short
 * explanation next to it. A step whose element is not on the page is skipped, and one inside the drawer opens the drawer
 * if it is hidden and hides it again once the tour has moved on, so the visitor's own choice is not changed.
 */
export const HelpTour = ({ steps, onClose }: Props): ReactNode => {
  const { t } = useTranslation();
  const titleId = useId();
  const [index, setIndex] = useState(0);
  // Which way the visitor is going, so a skipped step does not turn them around
  const direction = useRef<1 | -1>(1);
  const step = steps[index];

  const [drawerOpen, setDrawerOpen] = useSlideshowOpen();
  const openedDrawer = useRef(false);

  useEffect(() => {
    if (step?.needsDrawer && !drawerOpen && !openedDrawer.current) {
      openedDrawer.current = true;
      setDrawerOpen(true);
    } else if (!step?.needsDrawer && openedDrawer.current) {
      openedDrawer.current = false;
      setDrawerOpen(false);
    }
  }, [step, drawerOpen, setDrawerOpen]);

  useEffect(
    () => () => {
      if (openedDrawer.current) setDrawerOpen(false);
    },
    [setDrawerOpen],
  );

  const { rect, missing } = useHelpTarget(step?.target);

  useEffect(() => {
    if (!missing) return;

    const next = index + direction.current;
    if (next >= steps.length) {
      onClose();
    } else if (next < 0) {
      direction.current = 1;
      setIndex(index + 1);
    } else {
      setIndex(next);
    }
  }, [missing, index, steps.length, onClose]);

  // The callout follows the element: the anchor reads the latest box, and the popper is told when it moves
  const rectRef = useRef<TargetRect | null>(null);
  const popperRef = useRef<Instance | null>(null);
  const anchor = useMemo(() => ({ getBoundingClientRect: () => toDomRect(rectRef.current) }), []);
  useEffect(() => {
    rectRef.current = rect;
    void popperRef.current?.update();
  }, [rect]);

  if (!step) return null;

  const go = (by: 1 | -1) => {
    direction.current = by;
    const next = index + by;
    if (next >= steps.length) onClose();
    else setIndex(Math.max(next, 0));
  };

  const last = index === steps.length - 1;

  return (
    <Modal
      open
      hideBackdrop
      onClose={(_, reason) => {
        // A click beside the callout does not end the tour; Esc and the buttons do
        if (reason === 'escapeKeyDown') onClose();
      }}
    >
      <div tabIndex={-1} className="fixed inset-0 outline-none">
        {rect ? (
          <div
            aria-hidden
            className="pointer-events-none fixed rounded-lg border-2 border-amber-500 shadow-[0_0_0_9999px_rgba(2,6,23,0.78)]"
            style={{
              top: rect.top - PADDING,
              left: rect.left - PADDING,
              width: rect.width + PADDING * 2,
              height: rect.height + PADDING * 2,
            }}
          />
        ) : (
          <div aria-hidden className="fixed inset-0 bg-slate-950/80" />
        )}

        {rect && (
          <Popper
            open
            disablePortal
            anchorEl={anchor}
            popperRef={popperRef}
            placement="bottom"
            modifiers={[
              { name: 'offset', options: { offset: [0, CALLOUT_GAP] } },
              { name: 'flip', options: { fallbackPlacements: ['top', 'right', 'left'] } },
              { name: 'preventOverflow', options: { padding: 8 } },
            ]}
          >
            <div
              role="dialog"
              aria-labelledby={titleId}
              className="w-72 max-w-[calc(100vw-1rem)] rounded-lg border border-amber-500 bg-slate-900 p-3 text-slate-300 shadow-xl"
            >
              <div className="flex items-start justify-between gap-2">
                <h2 id={titleId} className="text-base font-semibold text-slate-100">
                  {t(`help.steps.${step.text}.title`)}
                </h2>
                <button
                  type="button"
                  onClick={onClose}
                  aria-label={t('common.close')}
                  title={t('common.close')}
                  className="shrink-0 -mt-0.5 -mr-1 p-0.5 text-slate-400 hover:text-slate-200 cursor-pointer"
                >
                  <CloseIcon fontSize="small" />
                </button>
              </div>
              <p className="mt-1 text-sm">{t(`help.steps.${step.text}.text`)}</p>
              <div className="mt-3 flex items-center justify-between gap-2">
                <span className="text-xs text-slate-500">
                  {t('help.progress', { current: index + 1, total: steps.length })}
                </span>
                <div className="flex gap-2">
                  {index > 0 && (
                    <button type="button" onClick={() => go(-1)} className={secondaryButtonClass}>
                      {t('help.back')}
                    </button>
                  )}
                  <button type="button" autoFocus onClick={() => go(1)} className={primaryButtonClass}>
                    {last ? t('help.done') : t('help.next')}
                  </button>
                </div>
              </div>
            </div>
          </Popper>
        )}
      </div>
    </Modal>
  );
};
