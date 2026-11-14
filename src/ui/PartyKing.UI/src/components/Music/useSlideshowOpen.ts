import { isMobileDevice } from '@/utils/isMobileDevice';
import { usePersistedFlag } from '@/utils/usePersistedFlag';

export const SLIDESHOW_OPEN_KEY = 'partyking.slideshowOpen';

/**
 * Whether the drawer with the slideshow or the lyrics is shown (hidden by default on a phone or tablet). The flag is
 * shared by every component that uses it, so the page and the help tour see one drawer.
 */
export const useSlideshowOpen = () => usePersistedFlag(SLIDESHOW_OPEN_KEY, !isMobileDevice());
