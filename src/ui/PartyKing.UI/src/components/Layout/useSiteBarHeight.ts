import { RefObject, useEffect } from 'react';

const VARIABLE = '--site-bar-h';

/**
 * Publishes the height of the site bar as `--site-bar-h`. The bar is in the page flow and wraps on a narrow screen, so
 * the pages that fill the screen (the game tables) take it off their height with `calc(100dvh - var(--site-bar-h))`
 * instead of guessing it.
 */
export const useSiteBarHeight = (bar: RefObject<HTMLElement | null>): void => {
  useEffect(() => {
    const element = bar.current;
    if (!element || typeof ResizeObserver === 'undefined') return;

    const publish = () => document.documentElement.style.setProperty(VARIABLE, `${element.offsetHeight}px`);
    publish();
    const observer = new ResizeObserver(publish);
    observer.observe(element);

    return () => {
      observer.disconnect();
      document.documentElement.style.removeProperty(VARIABLE);
    };
  }, [bar]);
};
