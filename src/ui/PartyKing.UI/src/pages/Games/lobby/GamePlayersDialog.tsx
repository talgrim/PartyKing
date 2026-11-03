import { ReactNode, useState } from 'react';
import {
  closestCenter,
  DndContext,
  DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import { arrayMove, SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from '@mui/material';
import { useTranslation } from 'react-i18next';

type Player = { id: string; name: string };

type RowProps = {
  player: Player;
  onTurn: boolean;
  onKick: () => void;
};

const PlayerRow = ({ player, onTurn, onKick }: RowProps): ReactNode => {
  const { t } = useTranslation();
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: player.id });

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`flex items-center gap-2 px-2 py-1 rounded-md bg-slate-700 ${isDragging ? 'opacity-70' : ''}`}
    >
      <button
        type="button"
        aria-label={t('lobby.dragLabel', { name: player.name })}
        className="px-1 cursor-grab touch-none text-slate-300"
        {...attributes}
        {...listeners}
      >
        ⠿
      </button>
      <span className="flex-1 truncate">
        {player.name}
        {onTurn && <span className="text-amber-400"> {t('lobby.onTurn')}</span>}
      </span>
      <Button size="small" color="error" onClick={onKick} aria-label={t('lobby.kickLabel', { name: player.name })}>
        {t('lobby.kick')}
      </Button>
    </li>
  );
};

type Props = {
  open: boolean;
  onClose: () => void;
  players: Player[];
  /** The player whose turn it is, marked in the list; none for a game without turns. */
  currentPlayerId?: string | null;
  /** What dragging changes, e.g. "the turn order". */
  orderLabel: string;
  /** Saves the new order; rejects when the players changed meanwhile. */
  onReorder: (reordered: Player[]) => Promise<unknown>;
  onKick: (player: Player) => Promise<unknown>;
  /** Fetches the game again after a failed change. */
  onReload: () => void;
};

/** Host and admins: the players of a game in order (drag to change it) and a way to kick a player. */
export const GamePlayersDialog = ({
  open,
  onClose,
  players,
  currentPlayerId = null,
  orderLabel,
  onReorder,
  onKick,
  onReload,
}: Props): ReactNode => {
  const { t } = useTranslation();
  const [message, setMessage] = useState<string>();
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const onDragEnd = async ({ active, over }: DragEndEvent) => {
    if (!over || active.id === over.id) return;
    const from = players.findIndex((player) => player.id === active.id);
    const to = players.findIndex((player) => player.id === over.id);
    setMessage(undefined);
    try {
      await onReorder(arrayMove(players, from, to));
    } catch {
      // Most likely a player joined or left meanwhile
      setMessage(t('lobby.changed'));
      onReload();
    }
  };

  const kick = async (player: Player) => {
    setMessage(undefined);
    try {
      await onKick(player);
    } catch {
      setMessage(t('lobby.kickFailed'));
      onReload();
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="xs"
      aria-labelledby="game-players-title"
      slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
    >
      <DialogTitle id="game-players-title">{t('lobby.players')}</DialogTitle>
      <DialogContent>
        {players.length === 0 ? (
          <p>{t('lobby.nobody')}</p>
        ) : (
          <>
            <p className="pb-2 text-slate-400">{t('lobby.dragHint', { order: orderLabel })}</p>
            <DndContext
              sensors={sensors}
              collisionDetection={closestCenter}
              onDragEnd={(event) => void onDragEnd(event)}
            >
              <SortableContext items={players.map((player) => player.id)} strategy={verticalListSortingStrategy}>
                <ul className="flex flex-col gap-1">
                  {players.map((player) => (
                    <PlayerRow
                      key={player.id}
                      player={player}
                      onTurn={player.id === currentPlayerId}
                      onKick={() => void kick(player)}
                    />
                  ))}
                </ul>
              </SortableContext>
            </DndContext>
          </>
        )}
        {message && <p className="pt-2 text-amber-500">{message}</p>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t('common.close')}</Button>
      </DialogActions>
    </Dialog>
  );
};
