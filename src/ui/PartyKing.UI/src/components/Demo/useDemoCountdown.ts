import { useMemo } from 'react';
import { DemoSessionDto } from '@/service/endpoints/demo/types';
import { useNow } from '@/utils/useNow';
import { CountdownLevel, countdownLevel, countdownRemainingMs } from './demoCountdown';

export type DemoCountdown = {
  remainingMs: number;
  level: CountdownLevel;
  /** The time is up. The server deletes the session within a minute of that. */
  ended: boolean;
};

/** Counts down to the end of a demo session by the server's clock; null while the end is not known. */
export const useDemoCountdown = (demo: DemoSessionDto | undefined): DemoCountdown | null => {
  const now = useNow(1000);

  // Taken once per answer, so a wrong clock on the device does not move the end
  const serverOffsetMs = useMemo(() => (demo ? Date.parse(demo.serverTime) - Date.now() : 0), [demo]);

  if (!demo) return null;

  const remainingMs = countdownRemainingMs(Date.parse(demo.expiresAt), serverOffsetMs, now);

  return { remainingMs, level: countdownLevel(remainingMs), ended: remainingMs <= 0 };
};
