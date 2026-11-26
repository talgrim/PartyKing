import { useMatch } from 'react-router-dom';
import { ROUTE_PATHS } from '@/router/constants';

/**
 * Whether the page is a game board that fills the screen (Ringo, Resistance), which has no room for a footer or a
 * notice: they would make it scroll or cover its controls.
 */
export const useHidesBottomBars = (): boolean => {
  const ringo = useMatch(ROUTE_PATHS.SESSION_GAMES_RINGO);
  const resistance = useMatch(ROUTE_PATHS.SESSION_GAMES_RESISTANCE);
  const panels = useMatch(ROUTE_PATHS.DEV_SESSION_RESISTANCE_PANELS);

  return Boolean(ringo || resistance || panels);
};
