import React, { ChangeEvent, ReactNode, useRef, useState } from "react"
import { axios } from "../service/request";
import envVariables from "@/envVariables";
import {Alert, Backdrop, Checkbox, FormControlLabel, FormGroup, LinearProgress} from '@mui/material';
import { makeUrlWithParams } from "@/service/fetcher/apiService";
import { UploadedImagePreview } from "@/components/UploadedImagePreview";

type UploadStatus = 'waiting' | 'uploading' | 'success' | 'error';

export const ImageUploader = (): ReactNode => {
    const [files, setFiles] = useState<File[]>([]);
    const [uploadProgress, setUploadProgress] = useState(0);
    const [uploadStatus, setUploadStatus] = useState<UploadStatus>('waiting');
    const fileInput = useRef(null);
    const [deleteAfterPresentation, setChecked] = React.useState(false);

    let API_URL = makeUrlWithParams(`${envVariables.apiUrl}/upload-photo/${deleteAfterPresentation}`);

    const handleCheckboxChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        setChecked(event.target.checked);
        console.log(deleteAfterPresentation);
        API_URL = makeUrlWithParams(`${envVariables.apiUrl}/upload-photo/${deleteAfterPresentation}`);
    };

    const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
        setUploadStatus('waiting');

        setFiles([
            ...e.target.files
        ]);
    }

    const handleSubmit = async () => {
        setUploadStatus('uploading');

        try {
            const mappedFiles = files.map((file) => new File([file], file.name));
            const formData = new FormData();
            for(let i = 0; i < mappedFiles.length; i++) {
                formData.append('files', mappedFiles[i]);
            }
            const response = await axios.put(API_URL.toString(), formData,
                {
                    headers: {
                        'Content-Type': `multipart/form-data`,
                    },
                    onUploadProgress: (progressEvent) => {
                        console.log(progressEvent);

                        if (progressEvent.total) {
                            setUploadProgress(Math.round((progressEvent.loaded * 100) / progressEvent.total));
                        }
                    }
                }
            );

            setUploadStatus('success');
            setUploadProgress(0);
            fileInput.current.value = null;
            setFiles([]);

            console.log(response);
        } catch (error) {
            setUploadStatus('error');

            console.log(error);
        }
    }

    return (
        <div className="flex flex-col items-center space-y-4 py-5 w-full">
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

            <div className="flex-flex-col rounded-xl bg-white-400 border-2 border-slate-500 border-dashed w-full font-bold text-slate-500 text-center p-5 relative">
                Wybierz zdjęcia
                <svg xmlns="http://www.w3.org/2000/svg" className="justify-self-center mt-6 w-12 h-12" height="2rem" viewBox="0 -960 960 960" width="2rem" fill="currentColor">
                    <path d="M480-480ZM200-120q-33 0-56.5-23.5T120-200v-560q0-33 23.5-56.5T200-840h320v80H200v560h560v-320h80v320q0 33-23.5 56.5T760-120H200Zm40-160h480L570-480 450-320l-90-120-120 160Zm440-320v-80h-80v-80h80v-80h80v80h80v80h-80v80h-80Z"/>
                </svg>
                <input ref={fileInput} id="file" type="file" onChange={handleChange} multiple={true} className="opacity-0 cursor-pointer absolute top-0 left-0 w-full h-full" accept=".jpg,.png,.bmp,.gif,.jpeg"/>
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
                        <UploadedImagePreview className="" image={file} key={file.name}/>
                    )
                })}
            </div>
        </div>
    )
}