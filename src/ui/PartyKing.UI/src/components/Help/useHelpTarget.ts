import { useEffect, useState } from 'react';
import { findHelpTarget, HelpTargetId } from './helpSteps';

export type TargetRect = { top: number; left: number; width: number; height: number };

type State = { id: HelpTargetId | undefined; rect: TargetRect | null; missing: boolean };

const NOT_FOUND: State = { id: undefined, rect: null, missing: false };

/** A target that is not on the page this long after its step began is not coming (it is not rendered for this viewer). */
const MISSING_AFTER_MS = 800;

const sameRect = (a: TargetRect | null, b: TargetRect) =>
  a !== null && a.top === b.top && a.left === b.left && a.width === b.width && a.height === b.height;

const prefersReducedMotion = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

/**
 * Follows the element a tour step points at: its box in the viewport, kept current while the page scrolls, resizes or
 * re-renders (a drawer that opens, a list that grows), by looking every frame. The element is scrolled into view once.
 * `missing` turns true when it does not show up, so the tour can skip the step.
 */
export const useHelpTarget = (id: HelpTargetId | undefined): Pick<State, 'rect' | 'missing'> => {
  const [state, setState] = useState<State>(NOT_FOUND);

  useEffect(() => {
    if (!id) return;

    const startedAt = performance.now();
    let scrolled = false;
    let frame = 0;

    const look = () => {
      const element = findHelpTarget(id);

      if (element) {
        if (!scrolled) {
          scrolled = true;
          element.scrollIntoView?.({ block: 'center', behavior: prefersReducedMotion() ? 'auto' : 'smooth' });
        }
        const { top, left, width, height } = element.getBoundingClientRect();
        const rect = { top, left, width, height };
        setState((previous) => (previous.id === id && sameRect(previous.rect, rect) ? previous : { id, rect, missing: false }));
      } else if (performance.now() - startedAt > MISSING_AFTER_MS) {
        setState((previous) => (previous.id === id && previous.missing ? previous : { id, rect: null, missing: true }));
      }

      frame = requestAnimationFrame(look);
    };

    frame = requestAnimationFrame(look);
    return () => cancelAnimationFrame(frame);
  }, [id]);

  // What was found for the previous step is not this step's
  return state.id === id ? state : NOT_FOUND;
};
