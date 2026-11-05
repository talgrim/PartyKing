import { ReactNode, useState } from 'react';
import {
  AssassinCardIcon,
  CardBackIcon,
  CommanderCardIcon,
  DefectorCardIcon,
  OperativeCardIcon,
  ReverserCardIcon,
  SpyCardIcon,
  SpyMarkerIcon,
} from '@/pages/Games/resistance/ResistanceIcons';
import { ResistanceCharacter, ResistancePlayerDto, ResistanceRole } from '@/service/endpoints/games/types';
import { useTranslation } from 'react-i18next';

type Props = {
  role: ResistanceRole;
  players: ResistancePlayerDto[];
  /** The spies the player knows: the other spies for a spy, every spy for the Commander. */
  fellowSpyIds: readonly string[];
  /** The Blind Spies variant: the spies do not know each other, so a spy is told there is no one to see. */
  blindSpies?: boolean;
  /** The spy Reverser keeps their eyes closed in the reveal (the rule is off): they are told they know no one. */
  hiddenReverserSpy?: boolean;
  /** The player's character (Reverser, Defector and Assassin modules), told next to the role while the card is face up. */
  character?: ResistanceCharacter | null;
  /** The other Defector, when the Defectors know each other: told to a Defector while the card is face up. */
  fellowDefectorName?: string | null;
};

/**
 * The player's secret role: a face-down card that turns over on a tap (and back on another, so a neighbour can't read
 * it). A spy also sees the other spies while the card is face up (or is told they don't know each
 * other, with the Blind Spies variant). The state is local and starts hidden after a reload.
 */
export const ResistanceRoleCard = ({ role, players, fellowSpyIds, blindSpies = false, hiddenReverserSpy = false, character = null, fellowDefectorName = null }: Props): ReactNode => {
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
        <>
          <ReverserCardIcon title={t('resistance.characters.reverser')} />
          <p className="max-w-48 text-sm text-amber-300">{t('resistance.roleCard.reverser')}</p>
        </>
      )}

      {revealed && character === 'Assassin' && (
        <>
          <AssassinCardIcon title={t('resistance.characters.assassin')} />
          <p className="max-w-48 text-sm text-amber-300">{t('resistance.roleCard.assassin')}</p>
        </>
      )}

      {revealed && character === 'Commander' && (
        <>
          <CommanderCardIcon title={t('resistance.characters.commander')} />
          <div className="max-w-48 text-sm text-amber-300">
            <p>{t('resistance.roleCard.commander')}</p>
            <ul aria-label={t('resistance.roleCard.commanderSpiesList')} className="text-slate-200">
              {spies.map((spy) => (
                <li key={spy.id} className="flex items-center gap-1">
                  <SpyMarkerIcon />
                  {spy.name}
                </li>
              ))}
            </ul>
          </div>
        </>
      )}

      {revealed && character === 'Defector' && <DefectorCardIcon title={t('resistance.characters.defector')} />}

      {revealed && character === 'Defector' && (
        <div className="max-w-48 text-sm text-amber-300">
          <p>{t('resistance.roleCard.defector')}</p>
          {fellowDefectorName != null && (
            <p>{t('resistance.roleCard.fellowDefector', { name: fellowDefectorName })}</p>
          )}
        </div>
      )}

      {revealed && role === 'Spy' && blindSpies && (
        <p className="max-w-48 text-sm text-slate-200">{t('resistance.roleCard.blindSpies')}</p>
      )}

      {revealed && role === 'Spy' && !blindSpies && hiddenReverserSpy && (
        <p className="max-w-48 text-sm text-slate-200">{t('resistance.roleCard.hiddenReverserSpy')}</p>
      )}

      {revealed && role === 'Spy' && !blindSpies && !hiddenReverserSpy && character === 'Defector' && (
        <p className="max-w-48 text-sm text-slate-200">{t('resistance.roleCard.defectorSpy')}</p>
      )}

      {revealed && role === 'Spy' && !blindSpies && !hiddenReverserSpy && character !== 'Defector' && (
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
