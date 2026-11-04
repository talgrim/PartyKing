import { ReactNode, useState } from 'react';
import { Button } from '@mui/material';
import { useTranslation } from 'react-i18next';

type Seat = { id: string; name: string };

type Props = {
  /** The seats a guest may take over; undefined while they load. */
  seats: Seat[] | undefined;
  /** Takes the seat over; rejects with status 409 when it is gone meanwhile. */
  claim: (playerId: string) => Promise<void>;
  onClaimed: () => void;
  /** Fetches the list again after a failed claim. */
  onReload: () => void;
};

/**
 * Where a player returns to a game that already started, from a device that has lost its place (a private tab that was
 * closed, a cleared cookie): the seats the host released, picked by name, without joining under a new one.
 */
export const GameSeatPicker = ({ seats, claim, onClaimed, onReload }: Props): ReactNode => {
  const { t } = useTranslation();
  const [busyId, setBusyId] = useState<string>();
  const [failed, setFailed] = useState(false);

  const take = async (seat: Seat) => {
    setBusyId(seat.id);
    setFailed(false);
    try {
      await claim(seat.id);
      onClaimed();
    } catch {
      setFailed(true);
      onReload();
    } finally {
      setBusyId(undefined);
    }
  };

  return (
    <section className="flex flex-col gap-3 mt-6" aria-labelledby="claim-seat-title">
      <h2 id="claim-seat-title" className="text-lg text-slate-200">
        {t('lobby.claim.title')}
      </h2>
      <p className="text-slate-400">{t('lobby.claim.hint')}</p>
      {failed && <p className="text-amber-500">{t('lobby.claim.failed')}</p>}
      {seats && seats.length === 0 && <p className="text-slate-400">{t('lobby.claim.empty')}</p>}
      <ul className="flex flex-col gap-2">
        {seats?.map((seat) => (
          <li key={seat.id} className="flex items-center gap-2 px-3 py-2 rounded-md bg-slate-700 text-slate-200">
            <span className="flex-1 truncate">{seat.name}</span>
            <Button
              size="small"
              variant="contained"
              disabled={busyId !== undefined}
              onClick={() => void take(seat)}
              aria-label={t('lobby.claim.takeLabel', { name: seat.name })}
            >
              {t('lobby.claim.take')}
            </Button>
          </li>
        ))}
      </ul>
    </section>
  );
};
