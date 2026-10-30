import { ReactNode, useState } from 'react';
import {
  CardBackIcon,
  OperativeCardIcon,
  SpyCardIcon,
  SpyMarkerIcon,
} from '@/pages/Games/resistance/ResistanceIcons';
import { ResistancePlayerDto, ResistanceRole } from '@/service/endpoints/games/types';

type Props = {
  role: ResistanceRole;
  players: ResistancePlayerDto[];
  /** The other spies; only a spy has them. */
  fellowSpyIds: readonly string[];
};

/**
 * The player's secret role: a face-down card that turns over on a tap (and back on another, so a neighbour can't read
 * it). A spy also sees the other spies while the card is face up. The state is local and starts hidden after a reload.
 */
export const ResistanceRoleCard = ({ role, players, fellowSpyIds }: Props): ReactNode => {
  const [revealed, setRevealed] = useState(false);
  const spies = players.filter((player) => fellowSpyIds.includes(player.id));

  return (
    <div className="flex items-center gap-4">
      <button
        type="button"
        onClick={() => setRevealed((value) => !value)}
        aria-label={revealed ? 'Ukryj swoją rolę' : 'Pokaż swoją rolę'}
        aria-pressed={revealed}
        className="cursor-pointer"
      >
        {!revealed && <CardBackIcon />}
        {revealed && role === 'Spy' && <SpyCardIcon title="Szpieg" />}
        {revealed && role === 'Resistance' && <OperativeCardIcon title="Ruch Oporu" />}
      </button>

      {revealed && role === 'Spy' && (
        <div>
          <h2 className="text-slate-200">Twoi współszpiedzy</h2>
          <ul aria-label="Fellow spies" className="text-sm">
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
