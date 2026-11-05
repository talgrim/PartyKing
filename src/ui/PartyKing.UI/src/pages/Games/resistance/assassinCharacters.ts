import { ResistanceGameDto } from '@/service/endpoints/games/types';

/** With this many players the Body Guard needs Deep Cover or the False Commander next to them. */
const BODY_GUARD_COVER_PLAYER_COUNT = 5;

/**
 * Why the optional characters of the Assassin module do not fit the players who joined so far (the same checks the
 * server makes at the start, so the host hears it before pressing "Start game"), or null when they fit or the module
 * is off. The Assassin takes one spy seat, so the other spy characters need one each of the rest.
 */
export const assassinCharactersProblem = (
  game: ResistanceGameDto,
): 'tooManySpyCharacters' | 'bodyGuardNeedsCover' | null => {
  const { rules } = game;
  if (!rules.assassinEnabled) return null;

  const players = game.players.length;
  const spies = rules.playerCounts.find((counts) => counts.playerCount === players)?.spies;
  if (spies == null) return null;

  const spyCharacters = [rules.deepCoverEnabled, rules.blindSpyEnabled, rules.falseCommanderEnabled].filter(Boolean).length;
  if (spyCharacters > spies - 1) return 'tooManySpyCharacters';

  if (
    rules.bodyGuardEnabled &&
    players === BODY_GUARD_COVER_PLAYER_COUNT &&
    !rules.deepCoverEnabled &&
    !rules.falseCommanderEnabled
  ) {
    return 'bodyGuardNeedsCover';
  }

  return null;
};
