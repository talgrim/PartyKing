import { ChangeEvent, ReactNode, useRef, useState } from 'react';
import { Alert, LinearProgress } from '@mui/material';
import { UploadedImagePreview } from '@/components/UploadedImagePreview';
import { i18n } from '@/i18n';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { settingsButtonClass } from './settingsStyles';
import { useTranslation } from 'react-i18next';

type UploadStatus = 'waiting' | 'uploading' | 'success' | 'error';

type Props = {
  sessionId: string;
  /** Tells the dialog around it whether an upload is running, so it can refuse to close meanwhile. */
  onBusyChange: (busy: boolean) => void;
  /** The session password was changed meanwhile: the visitor has to enter the new one again. */
  onUnauthorized: () => void;
};

// The API answers 400 with a readable reason (unsupported format, file too big) and 429 when a guest uploads too often
const describeUploadError = (error: unknown): string => {
  const { status, message } = (error as { status?: number; message?: string }) ?? {};

  if (status === HTTP_STATUS_CODE.BAD_REQUEST && message) return message;
  if (status === HTTP_STATUS_CODE.TOO_MANY_REQUESTS) return i18n.t('uploader.tooMany');
  if (status === HTTP_STATUS_CODE.PAYLOAD_TOO_LARGE) return i18n.t('uploader.tooBig');

  return i18n.t('uploader.failed');
};

/** Picks photos and uploads them to the session's slideshow. */
export const PhotoUploader = ({ sessionId, onBusyChange, onUnauthorized }: Props): ReactNode => {
  const { t } = useTranslation();
  const [files, setFiles] = useState<File[]>([]);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadStatus, setUploadStatus] = useState<UploadStatus>('waiting');
  const [errorMessage, setErrorMessage] = useState('');
  const [deleteAfterPresentation, setDeleteAfterPresentation] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    setUploadStatus('waiting');
    setErrorMessage('');
    setFiles([...(e.target.files ?? [])]);
  };

  const handleSubmit = async () => {
    setUploadStatus('uploading');
    onBusyChange(true);

    try {
      await sessionsApi.uploadPhotos(sessionId, files, deleteAfterPresentation, setUploadProgress);

      setUploadStatus('success');
      if (fileInput.current) fileInput.current.value = '';
      setFiles([]);
    } catch (error) {
      if ((error as { status?: number })?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        setUploadStatus('waiting');
        onUnauthorized();
        return;
      }

      setUploadStatus('error');
      setErrorMessage(describeUploadError(error));
    } finally {
      setUploadProgress(0);
      onBusyChange(false);
    }
  };

  const uploading = uploadStatus === 'uploading';

  return (
    <div className="space-y-4">
      {uploadStatus === 'success' && (
        <Alert variant="filled" severity="success">
          {t('uploader.done')}
        </Alert>
      )}
      {uploadStatus === 'error' && (
        <Alert variant="filled" severity="error">
          {errorMessage}
        </Alert>
      )}

      <div className="relative p-5 text-center font-semibold text-slate-400 border-2 border-slate-600 border-dashed rounded-xl hover:border-amber-600">
        {t('uploader.choose')}
        <svg
          xmlns="http://www.w3.org/2000/svg"
          className="mx-auto mt-4 w-12 h-12"
          viewBox="0 -960 960 960"
          fill="currentColor"
          aria-hidden="true"
        >
          <path d="M480-480ZM200-120q-33 0-56.5-23.5T120-200v-560q0-33 23.5-56.5T200-840h320v80H200v560h560v-320h80v320q0 33-23.5 56.5T760-120H200Zm40-160h480L570-480 450-320l-90-120-120 160Zm440-320v-80h-80v-80h80v-80h80v80h80v80h-80v80h-80Z" />
        </svg>
        {/* Invisible, but covers the whole box, so a tap anywhere in it opens the picker */}
        <input
          ref={fileInput}
          type="file"
          aria-label={t('uploader.choose')}
          onChange={handleChange}
          disabled={uploading}
          multiple
          accept=".jpg,.jpeg,.png,.gif,.webp"
          className="absolute top-0 left-0 w-full h-full opacity-0 cursor-pointer"
        />
      </div>

      <label className="flex items-center gap-3 text-sm text-slate-300">
        <input
          type="checkbox"
          className="size-4 accent-amber-600"
          checked={deleteAfterPresentation}
          onChange={(e) => setDeleteAfterPresentation(e.target.checked)}
          disabled={uploading}
        />
        {t('uploader.deleteAfter')}
      </label>

      {uploading && <LinearProgress variant="determinate" value={uploadProgress} aria-label={t('uploader.progress')} />}

      {files.length > 0 && (
        <button type="button" onClick={() => void handleSubmit()} disabled={uploading} className={settingsButtonClass}>
          {uploading ? t('uploader.uploading') : t('uploader.upload', { count: files.length })}
        </button>
      )}

      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {files.map((file) => (
          <UploadedImagePreview className="" image={file} key={`${file.name}-${file.size}-${file.lastModified}`} />
        ))}
      </div>
    </div>
  );
};
