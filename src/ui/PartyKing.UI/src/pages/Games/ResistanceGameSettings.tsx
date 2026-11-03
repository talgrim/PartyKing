import { ReactNode, useState } from 'react';
import CloseIcon from '@mui/icons-material/Close';
import { Dialog, DialogContent, DialogTitle } from '@mui/material';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { useResistanceGame } from '@/service/endpoints/games/useResistanceGame';
import { useTranslation } from 'react-i18next';

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
  const { t } = useTranslation();
  const { data: game, mutate } = useResistanceGame(sessionId);
  const [message, setMessage] = useState<string>();

  const started = game?.startedAt != null;

  const setPlotCards = async (enabled: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.setResistancePlotCards(sessionId, enabled), { revalidate: false });
    } catch {
      setMessage(t('resistance.settings.plotCardsFailed'));
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
        {t('resistance.settings.title')}
        <button
          type="button"
          onClick={onClose}
          aria-label={t('common.close')}
          title={t('common.close')}
          className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
        >
          <CloseIcon />
        </button>
      </DialogTitle>
      <DialogContent className="space-y-4">
        {started && <p className="text-sm text-amber-500">{t('resistance.settings.started')}</p>}
        <SettingsSection
          title={t('resistance.settings.plotCards.title')}
          description={t('resistance.settings.plotCards.description')}
        >
          <label className="flex items-center gap-2 text-slate-200">
            <input
              type="checkbox"
              checked={game?.rules.plotCardsEnabled ?? false}
              disabled={game === undefined || started}
              onChange={(event) => void setPlotCards(event.target.checked)}
            />
            {t('resistance.settings.plotCards.toggle')}
          </label>
        </SettingsSection>
        {message && <p className="text-amber-500">{message}</p>}
      </DialogContent>
    </Dialog>
  );
};
