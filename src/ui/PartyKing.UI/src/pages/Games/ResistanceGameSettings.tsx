import { ReactNode, useState } from 'react';
import CloseIcon from '@mui/icons-material/Close';
import { Dialog, DialogContent, DialogTitle } from '@mui/material';
import { gamesApi } from '@/service/endpoints/games/gamesApi';
import { ResistanceReverserSides } from '@/service/endpoints/games/types';
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
 * The plot cards add-on, the Inquisitor and Reverser modules and the Blind Spies variant exist. The modules can be changed until the game starts.
 */
export const ResistanceGameSettings = ({ sessionId, open, onClose }: Props): ReactNode => {
  const { t } = useTranslation();
  const { data: game, mutate } = useResistanceGame(sessionId);
  const [message, setMessage] = useState<string>();

  const started = game?.startedAt != null;
  // The sides only matter while the module is on, and like every setting they lock when the game starts
  const sidesDisabled = game === undefined || started || !game.rules.reverserEnabled;

  const setPlotCards = async (enabled: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.setResistancePlotCards(sessionId, enabled), { revalidate: false });
    } catch {
      setMessage(t('resistance.settings.plotCardsFailed'));
    }
  };

  const setInquisitor = async (enabled: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.setResistanceInquisitor(sessionId, enabled), { revalidate: false });
    } catch {
      setMessage(t('resistance.settings.inquisitorFailed'));
    }
  };

  const setBlindSpies = async (enabled: boolean) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.setResistanceBlindSpies(sessionId, enabled), { revalidate: false });
    } catch {
      setMessage(t('resistance.settings.blindSpiesFailed'));
    }
  };

  const setReverser = async (enabled: boolean, sides?: ResistanceReverserSides) => {
    setMessage(undefined);
    try {
      await mutate(gamesApi.setResistanceReverser(sessionId, enabled, sides), { revalidate: false });
    } catch {
      setMessage(t('resistance.settings.reverserFailed'));
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
        <SettingsSection
          title={t('resistance.settings.inquisitor.title')}
          description={t('resistance.settings.inquisitor.description')}
        >
          <label className="flex items-center gap-2 text-slate-200">
            <input
              type="checkbox"
              checked={game?.rules.inquisitorEnabled ?? false}
              disabled={game === undefined || started}
              onChange={(event) => void setInquisitor(event.target.checked)}
            />
            {t('resistance.settings.inquisitor.toggle')}
          </label>
        </SettingsSection>
        <SettingsSection
          title={t('resistance.settings.blindSpies.title')}
          description={t('resistance.settings.blindSpies.description')}
        >
          <label className="flex items-center gap-2 text-slate-200">
            <input
              type="checkbox"
              checked={game?.rules.blindSpiesEnabled ?? false}
              disabled={game === undefined || started}
              onChange={(event) => void setBlindSpies(event.target.checked)}
            />
            {t('resistance.settings.blindSpies.toggle')}
          </label>
        </SettingsSection>
        <SettingsSection
          title={t('resistance.settings.reverser.title')}
          description={t('resistance.settings.reverser.description')}
        >
          <label className="flex items-center gap-2 text-slate-200">
            <input
              type="checkbox"
              checked={game?.rules.reverserEnabled ?? false}
              disabled={game === undefined || started}
              onChange={(event) => void setReverser(event.target.checked)}
            />
            {t('resistance.settings.reverser.toggle')}
          </label>
          <label className={`flex flex-col gap-1 text-sm ${sidesDisabled ? 'text-slate-500' : 'text-slate-200'}`}>
            {t('resistance.settings.reverser.sides')}
            <select
              value={game?.rules.reverserSides ?? 'Both'}
              disabled={sidesDisabled}
              onChange={(event) => void setReverser(true, event.target.value as ResistanceReverserSides)}
              className="rounded bg-slate-700 p-1 text-slate-100 disabled:cursor-not-allowed disabled:bg-slate-800 disabled:text-slate-500 disabled:opacity-60"
            >
              <option value="Both">{t('resistance.settings.reverser.sidesBoth')}</option>
              <option value="Resistance">{t('resistance.settings.reverser.sidesResistance')}</option>
              <option value="Spy">{t('resistance.settings.reverser.sidesSpy')}</option>
            </select>
          </label>
        </SettingsSection>
        {message && <p className="text-amber-500">{message}</p>}
      </DialogContent>
    </Dialog>
  );
};
