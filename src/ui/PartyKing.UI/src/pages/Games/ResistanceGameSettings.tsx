import { ReactNode, useState } from 'react';
import CloseIcon from '@mui/icons-material/Close';
import { Dialog, DialogContent, DialogTitle } from '@mui/material';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceGame } from '@/service/endpoints/games/useResistanceGame';

type Props = {
  sessionId: string;
  open: boolean;
  onClose: () => void;
};

type SectionProps = {
  title: string;
  description: string;
  /** The module's settings, shown on the left. */
  children: ReactNode;
};

/** One module: its settings on the left and its description on the right (stacked on a narrow screen). */
const SettingsSection = ({ title, description, children }: SectionProps): ReactNode => (
  <section aria-label={title} className="p-4 bg-slate-900 rounded-lg">
    <h3 className="mb-3 text-xs font-semibold tracking-wider text-slate-400 uppercase">{title}</h3>
    <div className="grid gap-4 sm:grid-cols-2">
      <div className="space-y-2">{children}</div>
      <p className="text-sm text-slate-400">{description}</p>
    </div>
  </section>
);

/**
 * Host and admins: switches the optional modules of the game on and off, one section per module with a short description.
 * Only the plot cards add-on exists for now. The modules can be changed until the game starts.
 */
export const ResistanceGameSettings = ({ sessionId, open, onClose }: Props): ReactNode => {
  const { data: game, mutate } = useResistanceGame(sessionId);
  const [message, setMessage] = useState<string>();

  const started = game?.startedAt != null;

  const setPlotCards = async (enabled: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.setResistancePlotCards(sessionId, enabled), { revalidate: false });
    } catch {
      setMessage('Nie udało się zmienić ustawienia kart Intryg.');
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      aria-labelledby="resistance-settings-title"
      fullWidth
      maxWidth="sm"
      // MUI's emotion styles are unlayered and beat Tailwind's layered utilities, hence the important modifier
      slotProps={{ paper: { className: 'bg-slate-800! text-slate-200! rounded-xl!' } }}
    >
      <DialogTitle id="resistance-settings-title" className="flex! items-center justify-between text-xl! font-semibold!">
        Ustawienia gry
        <button
          type="button"
          onClick={onClose}
          aria-label="Close"
          title="Close"
          className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
        >
          <CloseIcon />
        </button>
      </DialogTitle>
      <DialogContent className="space-y-4">
        {started && <p className="text-sm text-amber-500">Gra już się rozpoczęła, więc ustawień nie można zmienić.</p>}
        <SettingsSection
          title="Karty Intryg"
          description="Dodatek, w którym Lider na początku każdej misji dobiera karty Intryg i rozdaje je innym graczom. Karty dają informacje o rolach albo zmieniają przebieg rundy: przejęcie przywództwa, unieważnienie głosowania, podgląd karty Misji."
        >
          <label className="flex items-center gap-2 text-slate-200">
            <input
              type="checkbox"
              checked={game?.rules.plotCardsEnabled ?? false}
              disabled={game === undefined || started}
              onChange={(event) => void setPlotCards(event.target.checked)}
            />
            Włącz karty Intryg
          </label>
        </SettingsSection>
        {message && <p className="text-amber-500">{message}</p>}
      </DialogContent>
    </Dialog>
  );
};
