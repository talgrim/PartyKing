import { ReactNode, useState } from 'react';
import CloseIcon from '@mui/icons-material/Close';
import SettingsIcon from '@mui/icons-material/Settings';
import { Dialog, DialogContent, DialogTitle } from '@mui/material';
import { useIsDemo } from '@/service/endpoints/demo/useDemoConfig';
import { SessionDto } from '@/service/endpoints/sessions/types';
import { AutoLyricsForm } from './AutoLyricsForm';
import { DeleteSessionForm } from './DeleteSessionForm';
import { FallbackPlaylistForm } from './FallbackPlaylistForm';
import { PhotoDownload } from './PhotoDownload';
import { SessionNameForm } from './SessionNameForm';
import { SessionPasswordChangeForm } from './SessionPasswordChangeForm';
import { SlideshowSettingsForm } from './SlideshowSettingsForm';
import { useTranslation } from 'react-i18next';
import { helpTarget } from '@/components/Help/helpSteps';

const SettingsSection = ({ title, children }: { title: string; children: ReactNode }): ReactNode => (
  <section aria-label={title} className="p-4 bg-slate-900 rounded-lg">
    <h3 className="mb-3 text-xs font-semibold tracking-wider text-slate-400 uppercase">{title}</h3>
    {children}
  </section>
);

type Props = {
  session: SessionDto;
  /** Called after a change that the session page shows, such as the name. */
  onChanged: () => void;
  /** Called after the session was deleted from the settings, to leave a page with nothing left to show. */
  onDeleted: () => void;
};

/**
 * Gear button with the session's settings in a dialog: name, password, fallback playlist, slideshow, photo download, lyrics and deleting the session. Meant for the host and
 * admins only; the API refuses everyone else.
 */
export const SessionSettings = ({ session, onChanged, onDeleted }: Props): ReactNode => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  // The fallback playlist is a Spotify one, and a demo session ends on its own
  const isDemo = useIsDemo();

  return (
    <>
      <button
        {...helpTarget('settings')}
        onClick={() => setOpen(true)}
        aria-label={t('sessionSettings.title')}
        title={t('sessionSettings.title')}
        className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
      >
        <SettingsIcon />
      </button>

      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        aria-labelledby="session-settings-title"
        fullWidth
        maxWidth="sm"
        // MUI's emotion styles are unlayered and beat Tailwind's layered utilities, hence the important modifier
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200! rounded-xl!' } }}
      >
        <DialogTitle id="session-settings-title" className="flex! items-center justify-between text-xl! font-semibold!">
          {t('sessionSettings.title')}
          <button
            onClick={() => setOpen(false)}
            aria-label={t('common.close')}
            title={t('common.close')}
            className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
          >
            <CloseIcon />
          </button>
        </DialogTitle>
        <DialogContent className="space-y-4">
          <SettingsSection title={t('sessionSettings.name')}>
            <SessionNameForm sessionId={session.id} currentName={session.name} onRenamed={onChanged} />
          </SettingsSection>
          <SettingsSection title={t('sessionSettings.password')}>
            <SessionPasswordChangeForm sessionId={session.id} />
          </SettingsSection>
          {!isDemo && (
            <SettingsSection title={t('sessionSettings.fallbackPlaylist')}>
              <FallbackPlaylistForm session={session} onChanged={onChanged} />
            </SettingsSection>
          )}
          <SettingsSection title={t('views.slideshow')}>
            <SlideshowSettingsForm sessionId={session.id} />
          </SettingsSection>
          <SettingsSection title={t('sessionSettings.photos')}>
            <PhotoDownload sessionId={session.id} />
          </SettingsSection>
          <SettingsSection title={t('views.lyrics')}>
            <AutoLyricsForm />
          </SettingsSection>
          {!isDemo && (
            <SettingsSection title={t('sessionDeletion.deleteTitle')}>
              <DeleteSessionForm sessionId={session.id} sessionName={session.name} onDeleted={onDeleted} />
            </SettingsSection>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
};
