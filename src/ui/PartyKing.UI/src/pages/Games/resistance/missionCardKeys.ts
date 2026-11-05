import { ResistanceMissionCardKind } from '@/service/endpoints/games/types';

/** The translation key of each mission card's name (literal keys, so `t` can check them). */
export const MISSION_CARD_KEYS = {
  Success: 'resistance.missionCard.success',
  Fail: 'resistance.missionCard.fail',
  Reverse: 'resistance.missionCard.reverse',
} as const satisfies Record<ResistanceMissionCardKind, string>;
