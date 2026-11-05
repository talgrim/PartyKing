import { ReactNode, useState } from 'react';
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from '@mui/material';
import { ResistancePlayerDto } from '@/service/endpoints/games/types';
import { useResistancePresence } from '@/service/endpoints/games/useResistancePresence';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
  open: boolean;
  onClose: () => void;
  players: ResistancePlayerDto[];
  /** Releases the seat of the player (or takes the release back); rejects when the server refuses. */
  onSetReleased: (player: ResistancePlayerDto, released: boolean) => Promise<unknown>;
};

/**
 * Host and admins: lets a player whose device is gone (a closed private tab) come back. Releasing the seat of an offline
 * player lets a guest take it over from the join page; the old device loses it for good. The seats of the host and of
 * admins are not listed, and a player who is online can't be released.
 */
export const ResistanceSeatsDialog = ({ sessionId, open, onClose, players, onSetReleased }: Props): ReactNode => {
  const { t } = useTranslation();
  const presence = useResistancePresence(sessionId, open);
  const [busyId, setBusyId] = useState<string>();
  const [failed, setFailed] = useState(false);

  const seats = players.filter((player) => !player.isManagerSeat);

  const toggle = async (player: ResistancePlayerDto) => {
    setBusyId(player.id);
    setFailed(false);
    try {
      await onSetReleased(player, !player.isSeatReleased);
    } catch {
      setFailed(true);
    } finally {
      setBusyId(undefined);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="xs"
      aria-labelledby="resistance-seats-title"
      // MUI's emotion styles are unlayered and beat Tailwind's layered utilities, hence the important modifier
      slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
    >
      <DialogTitle id="resistance-seats-title">{t('resistance.seats.title')}</DialogTitle>
      <DialogContent className="space-y-3">
        <p className="text-slate-400">{t('resistance.seats.hint')}</p>
        {failed && <p className="text-amber-500">{t('resistance.seats.failed')}</p>}
        {seats.length === 0 && <p>{t('resistance.seats.empty')}</p>}
        <ul className="space-y-2">
          {seats.map((player) => {
            // No answer yet (or none for a viewer that can't ask) leaves the status out and the button enabled
            const online = presence?.onlinePlayerIds.includes(player.id);
            return (
              <li key={player.id} className="flex items-center gap-2">
                <span className="flex-1 truncate">{player.name}</span>
                {online !== undefined && (
                  <span className={online ? 'text-green-400' : 'text-slate-400'}>
                    {online ? t('resistance.presence.online') : t('resistance.presence.offline')}
                  </span>
                )}
                {player.isSeatReleased && <span className="text-amber-500">{t('resistance.seats.released')}</span>}
                <Button
                  size="small"
                  variant={player.isSeatReleased ? 'outlined' : 'contained'}
                  disabled={busyId !== undefined || (!player.isSeatReleased && online === true)}
                  onClick={() => void toggle(player)}
                  aria-label={t(player.isSeatReleased ? 'resistance.seats.keepLabel' : 'resistance.seats.releaseLabel', { name: player.name })}
                >
                  {player.isSeatReleased ? t('resistance.seats.keep') : t('resistance.seats.release')}
                </Button>
              </li>
            );
          })}
        </ul>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t('common.close')}</Button>
      </DialogActions>
    </Dialog>
  );
};
