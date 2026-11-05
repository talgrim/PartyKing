import { usePersistedNumber } from '@/utils/usePersistedNumber';

const VOLUME_STORAGE_KEY = 'partyking.volume';
const DEFAULT_VOLUME = 0.5;

/**
 * The volume of the host's player, 0 to 1, kept in the browser's localStorage. The tab that plays the music and any
 * other tab of the same browser (the Resistance page's queue panel) share it, so a change in one reaches the other.
 */
export const usePlayerVolume = () => usePersistedNumber(VOLUME_STORAGE_KEY, DEFAULT_VOLUME, 0, 1);
