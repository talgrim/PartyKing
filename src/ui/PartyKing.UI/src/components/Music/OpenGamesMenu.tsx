import { ReactNode, useState } from 'react';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import SportsEsportsIcon from '@mui/icons-material/SportsEsports';
import {
  sessionResistanceJoinPath,
  sessionResistancePath,
  sessionRingoJoinPath,
  sessionRingoPath,
} from '@/router/constants';
import { useResistanceLobby } from '@/service/endpoints/games/useResistanceLobby';
import { useRingoLobby } from '@/service/endpoints/games/useRingoLobby';
import { withDevPersona } from '@/service/devPersona';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
  /**
   * Host or admin: they go straight to the board; for Resistance they get a second tab to play in (the first is for
   * the TV). Everyone else goes to the page where they join.
   */
  canManage: boolean;
};

/**
 * The Resistance tabs: the TV board and, unless the game started without the caller, the page to play on. A started
 * game locks out everyone who did not join, so a host or admin who did not has only the TV tab.
 */
const resistancePaths = (sessionId: string, canManage: boolean, lobby: { isStarted: boolean; joined: boolean }) => {
  if (!canManage) return [sessionResistanceJoinPath(sessionId)];

  const tv = `${sessionResistancePath(sessionId)}?tv=1`;

  return lobby.isStarted && !lobby.joined ? [tv] : [tv, sessionResistanceJoinPath(sessionId)];
};

/** A gamepad icon with a menu of the games open in the session; hidden while none is. A game opens in a new tab. */
export const OpenGamesMenu = ({ sessionId, canManage }: Props): ReactNode => {
  const { t } = useTranslation();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const { data: ringo } = useRingoLobby(sessionId);
  const { data: resistance } = useResistanceLobby(sessionId);

  const games = [
    ...(ringo?.isOpen
      ? [{ name: 'Ringo', paths: [canManage ? sessionRingoPath(sessionId) : sessionRingoJoinPath(sessionId)] }]
      : []),
    ...(resistance?.isOpen
      ? [{ name: 'Resistance', paths: resistancePaths(sessionId, canManage, resistance) }]
      : []),
  ];

  if (games.length === 0) return null;

  const play = (paths: string[]) => {
    setAnchor(null);
    paths.forEach((path) => window.open(withDevPersona(path), '_blank', 'noopener,noreferrer'));
  };

  return (
    <>
      <button
        onClick={(event) => setAnchor(event.currentTarget)}
        aria-label={t('openGames.label')}
        title={t('openGames.label')}
        aria-haspopup="menu"
        className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
      >
        <SportsEsportsIcon />
      </button>
      <Menu
        anchorEl={anchor}
        open={anchor !== null}
        onClose={() => setAnchor(null)}
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        {games.map((game) => (
          <MenuItem key={game.name} onClick={() => play(game.paths)}>
            {game.name}
          </MenuItem>
        ))}
      </Menu>
    </>
  );
};
