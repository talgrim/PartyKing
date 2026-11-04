import useSWR from 'swr';
import { resistanceClaimableSeatsKey } from '@/service/realtime/sessionMusicEvents';
import { gamesApi } from './gamesApi';

const CLAIMABLE_SEATS_REFRESH_MS = 10_000;

/**
 * The seats of a started Resistance game that a guest may take over. The hub revalidates it with the lobby pushes (a seat
 * released or claimed) and it is polled every 10 s, because a player coming back online is not pushed; a stale list is
 * also corrected by the claim's own answer.
 */
export const useResistanceClaimableSeats = (sessionId: string, enabled: boolean) => {
  const { data, ...rest } = useSWR(enabled ? resistanceClaimableSeatsKey(sessionId) : null, () => gamesApi.getResistanceClaimableSeats(sessionId), {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
    // A player coming back online is not pushed, so the list is also checked now and then
    refreshInterval: CLAIMABLE_SEATS_REFRESH_MS,
  });

  return { ...rest, seats: data };
};
