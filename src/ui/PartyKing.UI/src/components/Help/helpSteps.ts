/** Names of the elements the tour can point at; each is put on the real element with `helpTarget`. */
export type HelpTargetId =
  | 'demoBanner'
  | 'games'
  | 'photos'
  | 'qr'
  | 'settings'
  | 'nowPlaying'
  | 'player'
  | 'queueAdd'
  | 'queueList'
  | 'slideshow'
  | 'drawerControls'
  | 'language'
  | 'bac';

/** Keys under `help.steps` in the locale files. */
export type HelpStepText =
  | 'demoBanner'
  | 'games'
  | 'photos'
  | 'qr'
  | 'settings'
  | 'nowPlaying'
  | 'player'
  | 'queueAdd'
  | 'queueListHost'
  | 'queueListGuest'
  | 'slideshow'
  | 'lyrics'
  | 'drawerControlsHost'
  | 'drawerControlsGuest'
  | 'language'
  | 'bac';

export type HelpStep = {
  target: HelpTargetId;
  text: HelpStepText;
  /** Who gets the step: everybody, or only the host. */
  audience: 'all' | 'host' | 'guest';
  /** The step points into the drawer, which the tour opens when it is hidden. */
  needsDrawer?: boolean;
};

/**
 * The tour, in the order of the page from the top. A step whose target is not on the page (an empty queue has no
 * rows, a maximized page hides everything behind it) is skipped by the tour, not shown without a pointer.
 */
export const HELP_STEPS: readonly HelpStep[] = [
  { target: 'demoBanner', text: 'demoBanner', audience: 'all' },
  { target: 'games', text: 'games', audience: 'all' },
  { target: 'photos', text: 'photos', audience: 'all' },
  { target: 'qr', text: 'qr', audience: 'host' },
  { target: 'settings', text: 'settings', audience: 'host' },
  { target: 'nowPlaying', text: 'nowPlaying', audience: 'all' },
  { target: 'player', text: 'player', audience: 'host' },
  { target: 'queueAdd', text: 'queueAdd', audience: 'all' },
  { target: 'queueList', text: 'queueListHost', audience: 'host' },
  { target: 'queueList', text: 'queueListGuest', audience: 'guest' },
  { target: 'slideshow', text: 'slideshow', audience: 'host', needsDrawer: true },
  { target: 'slideshow', text: 'lyrics', audience: 'guest', needsDrawer: true },
  { target: 'drawerControls', text: 'drawerControlsHost', audience: 'host' },
  { target: 'drawerControls', text: 'drawerControlsGuest', audience: 'guest' },
  { target: 'language', text: 'language', audience: 'all' },
  { target: 'bac', text: 'bac', audience: 'all' },
];

export const stepsFor = (isHost: boolean): HelpStep[] =>
  HELP_STEPS.filter((step) => step.audience === 'all' || step.audience === (isHost ? 'host' : 'guest'));

/** Spread onto the element a step points at: `<button {...helpTarget('games')}>`. */
export const helpTarget = (id: HelpTargetId) => ({ 'data-help': id });

export const findHelpTarget = (id: HelpTargetId): HTMLElement | null =>
  document.querySelector<HTMLElement>(`[data-help="${id}"]`);
