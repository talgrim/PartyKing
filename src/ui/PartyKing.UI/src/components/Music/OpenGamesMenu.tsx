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

type Props = {
  sessionId: string;
  /** Host or admin: they go straight to the board, everyone else to the page where they join. */
  canManage: boolean;
};

/** A gamepad icon with a menu of the games open in the session; hidden while none is. A game opens in a new tab. */
export const OpenGamesMenu = ({ sessionId, canManage }: Props): ReactNode => {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const { data: ringo } = useRingoLobby(sessionId);
  const { data: resistance } = useResistanceLobby(sessionId);

  const games = [
    ...(ringo?.isOpen
      ? [{ name: 'Ringo', path: canManage ? sessionRingoPath(sessionId) : sessionRingoJoinPath(sessionId) }]
      : []),
    ...(resistance?.isOpen
      ? [{ name: 'Resistance', path: canManage ? sessionResistancePath(sessionId) : sessionResistanceJoinPath(sessionId) }]
      : []),
  ];

  if (games.length === 0) return null;

  const play = (path: string) => {
    setAnchor(null);
    window.open(withDevPersona(path), '_blank', 'noopener,noreferrer');
  };

  return (
    <>
      <button
        onClick={(event) => setAnchor(event.currentTarget)}
        aria-label="Open games"
        title="Open games"
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
          <MenuItem key={game.name} onClick={() => play(game.path)}>
            {game.name}
          </MenuItem>
        ))}
      </Menu>
    </>
  );
};
