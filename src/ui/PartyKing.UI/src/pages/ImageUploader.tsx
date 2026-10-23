import React, { ChangeEvent, ReactNode, useRef, useState } from "react"
import { useParams } from "react-router-dom";
import useSWR from "swr";
import {Alert, Backdrop, Checkbox, FormControlLabel, FormGroup, LinearProgress} from '@mui/material';
import { UploadedImagePreview } from "@/components/UploadedImagePreview";
import { PageNavigation } from "@/components/common/PageNavigation";
import { SessionPasswordForm } from "@/components/Music/SessionPasswordForm";
import { ROUTE_PATHS, sessionPath, sessionSlideshowPath } from "@/router/constants";
import { HTTP_STATUS_CODE } from "@/service/constants";
import { sessionsApi } from "@/service/endpoints/sessions/sessionsApi";

type UploadStatus = 'waiting' | 'uploading' | 'success' | 'error';

// The API answers 400 with a readable reason (unsupported format, file too big) and 429 when a guest uploads too often
const describeUploadError = (error: unknown): string => {
    const { status, message } = (error as { status?: number; message?: string }) ?? {};

    if (status === HTTP_STATUS_CODE.BAD_REQUEST && message) return message;
    if (status === HTTP_STATUS_CODE.TOO_MANY_REQUESTS) return 'Zbyt wiele prób. Spróbuj ponownie za minutę.';
    if (status === HTTP_STATUS_CODE.PAYLOAD_TOO_LARGE) return 'Pliki są za duże (maks. 20 MB na zdjęcie, 100 MB naraz).';

    return 'Nie udało się wrzucić plików. Spróbuj ponownie.';
};

/** Guests add photos to the slideshow of one session; the session password is asked for first. */
export const ImageUploader = (): ReactNode => {
    const { sessionId = '' } = useParams();
    const { error, mutate } = useSWR(['session', sessionId], () => sessionsApi.getSession(sessionId), {
        shouldRetryOnError: false,
    });
    const locked = (error as { status?: number } | undefined)?.status === HTTP_STATUS_CODE.UNAUTHORIZED;

    const [files, setFiles] = useState<File[]>([]);
    const [uploadProgress, setUploadProgress] = useState(0);
    const [uploadStatus, setUploadStatus] = useState<UploadStatus>('waiting');
    const [errorMessage, setErrorMessage] = useState('');
    const fileInput = useRef<HTMLInputElement>(null);
    const [deleteAfterPresentation, setChecked] = React.useState(false);

    const handleCheckboxChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        setChecked(event.target.checked);
    };

    const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
        setUploadStatus('waiting');
        setErrorMessage('');

        setFiles([
            ...(e.target.files ?? [])
        ]);
    }

    const handleSubmit = async () => {
        setUploadStatus('uploading');

        try {
            await sessionsApi.uploadPhotos(sessionId, files, deleteAfterPresentation, setUploadProgress);

            setUploadStatus('success');
            setUploadProgress(0);
            if (fileInput.current) fileInput.current.value = '';
            setFiles([]);
        } catch (error) {
            if ((error as { status?: number })?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
                // The password was changed meanwhile
                setUploadStatus('waiting');
                void mutate();
                return;
            }

            setUploadStatus('error');
            setErrorMessage(describeUploadError(error));
        }
    }

    const navigation = (
        <PageNavigation links={[
            { to: sessionSlideshowPath(sessionId), label: "Slideshow" },
            { to: sessionPath(sessionId), label: "Session" },
            { to: ROUTE_PATHS.HOME, label: "Sessions" },
        ]}/>
    );

    if (locked) {
        return (
            <div className="flex flex-col items-center space-y-4 py-5 w-full">
                {navigation}
                <SessionPasswordForm sessionId={sessionId} onJoined={() => void mutate()} />
            </div>
        );
    }

    return (
        <div className="flex flex-col items-center space-y-4 py-5 w-full">
            {navigation}

            { uploadStatus === 'uploading' &&
                <Backdrop open>
                    <div className="w-3xl">
                        <LinearProgress className="w-full" variant="determinate" value={uploadProgress} />
                    </div>
                </Backdrop>
            }

            { uploadStatus === 'success' &&
                <Alert variant="filled" severity="success">
                    Pliki wrzucone
                </Alert>
            }

            { uploadStatus === 'error' &&
                <Alert variant="filled" severity="error">
                    {errorMessage}
                </Alert>
            }

            <div className="flex-flex-col rounded-xl bg-white-400 border-2 border-slate-500 border-dashed w-full font-bold text-slate-500 text-center p-5 relative">
                Wybierz zdjęcia
                <svg xmlns="http://www.w3.org/2000/svg" className="justify-self-center mt-6 w-12 h-12" height="2rem" viewBox="0 -960 960 960" width="2rem" fill="currentColor">
                    <path d="M480-480ZM200-120q-33 0-56.5-23.5T120-200v-560q0-33 23.5-56.5T200-840h320v80H200v560h560v-320h80v320q0 33-23.5 56.5T760-120H200Zm40-160h480L570-480 450-320l-90-120-120 160Zm440-320v-80h-80v-80h80v-80h80v80h80v80h-80v80h-80Z"/>
                </svg>
                <input ref={fileInput} id="file" type="file" onChange={handleChange} multiple={true} className="opacity-0 cursor-pointer absolute top-0 left-0 w-full h-full" accept=".jpg,.jpeg,.png,.gif,.webp"/>
            </div>

            <FormGroup>
                <FormControlLabel control={
                  <Checkbox
                    color="success"
                    checked={deleteAfterPresentation}
                    onChange={handleCheckboxChange}
                    sx={{
                    color: "#DFE0EB",
                    "&.Mui-checked": {
                      color: "#1DFB9D"
                    }}}
                  />}
                 label="Usuń po wyświetleniu" />
            </FormGroup>

            {
                files.length > 0 && <button type="button" onClick={handleSubmit} className="flex rounded-xl bg-green-700 text-green-200 py-2 px-4">
                    Prześlij
                </button>
            }

            <div className="grid grid-cols-3 gap-3">
                {files.map((file: File): ReactNode => {
                    return (
                        <UploadedImagePreview className="" image={file} key={`${file.name}-${file.size}-${file.lastModified}`}/>
                    )
                })}
            </div>
        </div>
    )
}