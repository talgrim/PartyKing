import { ReactNode, useState } from 'react';
import CloseIcon from '@mui/icons-material/Close';
import { Dialog, DialogContent, DialogTitle, useMediaQuery } from '@mui/material';
import { PhotoUploader } from './PhotoUploader';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
  open: boolean;
  onClose: () => void;
  /** The session password was changed during the upload. */
  onUnauthorized: () => void;
};

// Tailwind's `sm` breakpoint: below it the dialog fills the screen, which suits a phone
const PHONE_QUERY = '(max-width: 639px)';

/**
 * Dialog for guests to add photos to the session's slideshow. It stays on the session page, so the host's player
 * keeps playing, and it can't be closed while an upload runs.
 */
export const PhotoUploadDialog = ({ sessionId, open, onClose, onUnauthorized }: Props): ReactNode => {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const fullScreen = useMediaQuery(PHONE_QUERY);

  const close = () => {
    if (!busy) onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={close}
      aria-labelledby="photo-upload-title"
      fullWidth
      maxWidth="sm"
      fullScreen={fullScreen}
      // MUI's emotion styles are unlayered and beat Tailwind's layered utilities, hence the important modifier
      slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
    >
      <DialogTitle id="photo-upload-title" className="flex! items-center justify-between text-xl! font-semibold!">
        {t('photoUpload.title')}
        <button
          onClick={close}
          disabled={busy}
          aria-label={t('common.close')}
          title={t('common.close')}
          className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer disabled:opacity-40 disabled:cursor-default"
        >
          <CloseIcon />
        </button>
      </DialogTitle>
      <DialogContent>
        <PhotoUploader sessionId={sessionId} onBusyChange={setBusy} onUnauthorized={onUnauthorized} />
      </DialogContent>
    </Dialog>
  );
};
