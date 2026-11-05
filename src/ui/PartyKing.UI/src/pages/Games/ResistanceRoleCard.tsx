import { ReactNode, useState } from 'react';
import {
  CardBackIcon,
  OperativeCardIcon,
  SpyCardIcon,
  SpyMarkerIcon,
} from '@/pages/Games/resistance/ResistanceIcons';
import { ResistanceCharacter, ResistancePlayerDto, ResistanceRole } from '@/service/endpoints/games/types';
import { useTranslation } from 'react-i18next';

type Props = {
  role: ResistanceRole;
  players: ResistancePlayerDto[];
  /** The other spies; only a spy has them. */
  fellowSpyIds: readonly string[];
  /** The Blind Spies variant: the spies do not know each other, so a spy is told there is no one to see. */
  blindSpies?: boolean;
  /** The spy Reverser keeps their eyes closed in the reveal (the rule is off): they are told they know no one. */
  hiddenReverserSpy?: boolean;
  /** The Reverser module: the player's character, told next to the role while the card is face up. */
  character?: ResistanceCharacter | null;
};

/**
 * The player's secret role: a face-down card that turns over on a tap (and back on another, so a neighbour can't read
 * it). A spy also sees the other spies while the card is face up (or is told they don't know each
 * other, with the Blind Spies variant). The state is local and starts hidden after a reload.
 */
export const ResistanceRoleCard = ({ role, players, fellowSpyIds, blindSpies = false, hiddenReverserSpy = false, character = null }: Props): ReactNode => {
  const { t } = useTranslation();
  const [revealed, setRevealed] = useState(false);
  const spies = players.filter((player) => fellowSpyIds.includes(player.id));

  return (
    <div className="flex items-center gap-4">
      <button
        type="button"
        onClick={() => setRevealed((value) => !value)}
        aria-label={revealed ? t('resistance.roleCard.hide') : t('resistance.roleCard.show')}
        aria-pressed={revealed}
        className="cursor-pointer"
      >
        {!revealed && <CardBackIcon />}
        {revealed && role === 'Spy' && <SpyCardIcon title={t('resistance.roles.spy')} />}
        {revealed && role === 'Resistance' && <OperativeCardIcon title={t('resistance.roles.resistance')} />}
      </button>

      {revealed && character === 'Reverser' && (
        <p className="max-w-48 text-sm text-amber-300">{t('resistance.roleCard.reverser')}</p>
      )}

      {revealed && role === 'Spy' && blindSpies && (
        <p className="max-w-48 text-sm text-slate-200">{t('resistance.roleCard.blindSpies')}</p>
      )}

      {revealed && role === 'Spy' && !blindSpies && hiddenReverserSpy && (
        <p className="max-w-48 text-sm text-slate-200">{t('resistance.roleCard.hiddenReverserSpy')}</p>
      )}

      {revealed && role === 'Spy' && !blindSpies && !hiddenReverserSpy && (
        <div>
          <h2 className="text-slate-200">{t('resistance.roleCard.fellowSpies')}</h2>
          <ul aria-label={t('resistance.roleCard.fellowSpiesList')} className="text-sm">
            {spies.map((spy) => (
              <li key={spy.id} className="flex items-center gap-1">
                <SpyMarkerIcon />
                {spy.name}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};
