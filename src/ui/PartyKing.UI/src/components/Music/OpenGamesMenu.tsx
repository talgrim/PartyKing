import { ReactNode, useState } from 'react';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import SportsEsportsIcon from '@mui/icons-material/SportsEsports';
import {
  sessionGamesPath,
  sessionResistanceJoinPath,
  sessionResistancePath,
  sessionRingoJoinPath,
  sessionRingoPath,
} from '@/router/constants';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceLobby } from '@/service/endpoints/games/useResistanceLobby';
import { useRingoLobby } from '@/service/endpoints/games/useRingoLobby';
import { withDevPersona } from '@/service/devPersona';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
  /**
   * Host or admin: they see every game, create a lobby that does not exist yet and go straight to the board; for an
   * open Resistance game they get a second tab to play in (the first is for the TV). Everyone else goes to the page
   * where they join, and only to a game the host opened.
   */
  canManage: boolean;
};

type Game = {
  name: string;
  /** Whether the session has a lobby of the game; the host sees it next to the name. */
  exists: boolean;
  /** Open to guests, which is what enables the row for them. */
  isOpen: boolean;
  /** The tabs the row opens once the game has a lobby (or always, when it has no lobby to create). */
  paths: string[];
  /** Creates the lobby on a click; a game without it (or with a lobby already) just opens its paths. */
  create?: () => Promise<unknown>;
  /** The paths once the lobby was just created, which is still closed. */
  createdPaths?: string[];
  /** Only the host and admins see the row. */
  managersOnly?: boolean;
};

/**
 * The Resistance tabs: the TV board and, once the game is open and unless it started without the caller, the page to
 * play on. A closed game cannot be joined yet and a started one locks out everyone who did not join, so those get the
 * TV tab only.
 */
const resistancePaths = (sessionId: string, canManage: boolean, lobby: { isOpen: boolean; isStarted: boolean; joined: boolean }) => {
  if (!canManage) return [sessionResistanceJoinPath(sessionId)];

  const tv = `${sessionResistancePath(sessionId)}?tv=1`;

  return !lobby.isOpen || (lobby.isStarted && !lobby.joined) ? [tv] : [tv, sessionResistanceJoinPath(sessionId)];
};

/**
 * A gamepad icon with a menu of the games, always shown. The host and admins see all of them, with a mark on those
 * that have a lobby, and a click creates a missing lobby; the others get a disabled icon until the host opens a game,
 * then the games, of which only the open ones can be chosen. A game opens in a new tab.
 */
export const OpenGamesMenu = ({ sessionId, canManage }: Props): ReactNode => {
  const { t } = useTranslation();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [busy, setBusy] = useState(false);
  const { data: ringo, mutate: mutateRingo } = useRingoLobby(sessionId);
  const { data: resistance, mutate: mutateResistance } = useResistanceLobby(sessionId);

  const games: Game[] = [
    {
      name: 'Ringo',
      exists: ringo !== undefined,
      isOpen: ringo?.isOpen === true,
      paths: [canManage ? sessionRingoPath(sessionId) : sessionRingoJoinPath(sessionId)],
      create: ringo === undefined && canManage ? () => gamesApi.startRingo(sessionId).then(() => mutateRingo()) : undefined,
      createdPaths: [sessionRingoPath(sessionId)],
    },
    {
      name: 'Resistance',
      exists: resistance !== undefined,
      isOpen: resistance?.isOpen === true,
      paths: resistance ? resistancePaths(sessionId, canManage, resistance) : [],
      create:
        resistance === undefined && canManage
          ? () => gamesApi.createResistance(sessionId).then(() => mutateResistance())
          : undefined,
      createdPaths: [`${sessionResistancePath(sessionId)}?tv=1`],
    },
    {
      name: t('openGames.neverHaveIEver'),
      exists: false,
      isOpen: false,
      paths: [sessionGamesPath(sessionId)],
      managersOnly: true,
    },
  ];

  const visible = games.filter((game) => canManage || !game.managersOnly);
  const enabled = canManage || visible.some((game) => game.isOpen);

  const open = (paths: string[]) => {
    paths.forEach((path) => window.open(withDevPersona(path), '_blank', 'noopener,noreferrer'));
  };

  const play = async (game: Game) => {
    setAnchor(null);
    if (!game.create) {
      open(game.paths);

      return;
    }

    setBusy(true);
    try {
      await game.create();
      open(game.createdPaths ?? game.paths);
    } catch (error) {
      // A game that was started meanwhile (409) is just there to open; anything else leaves the menu as it was
      if ((error as { status?: number }).status === 409) {
        await Promise.all([mutateRingo(), mutateResistance()]);
        open(game.createdPaths ?? game.paths);
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <button
        onClick={(event) => setAnchor(event.currentTarget)}
        aria-label={t('openGames.label')}
        title={enabled ? t('openGames.label') : t('openGames.waiting')}
        aria-haspopup="menu"
        disabled={!enabled}
        className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer disabled:opacity-40 disabled:cursor-default disabled:hover:text-slate-400"
      >
        <SportsEsportsIcon />
      </button>
      <Menu
        anchorEl={anchor}
        open={anchor !== null}
        onClose={() => setAnchor(null)}
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        {visible.map((game) => (
          <MenuItem
            key={game.name}
            disabled={busy || (!canManage && !game.isOpen)}
            onClick={() => void play(game)}
            className="gap-4 justify-between"
          >
            {game.name}
            {canManage && game.exists && ' '}
            {canManage && game.exists && (
              <span className={`text-xs ${game.isOpen ? 'text-emerald-400' : 'text-slate-400'}`}>
                {game.isOpen ? t('openGames.open') : t('openGames.lobby')}
              </span>
            )}
          </MenuItem>
        ))}
      </Menu>
    </>
  );
};
