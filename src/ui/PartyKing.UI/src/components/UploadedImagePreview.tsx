import { ReactNode, useEffect, useMemo } from "react"
import { useTranslation } from 'react-i18next';

type ImagePreviewType = {
    image: File,
    className?: string
}

export const UploadedImagePreview = ({ image, className }: ImagePreviewType): ReactNode => {
    const { t } = useTranslation();
    const url = useMemo(() => URL.createObjectURL(image), [image]);

    useEffect(() => () => URL.revokeObjectURL(url), [url]);

    return (
        <div className={`p-4 rounded-xl bg-neutral-800 text-neutral-400 ${className}`}>
            <img src={url} className="w-full h-auto mb-4" />
            <p>{t('imagePreview.name')} <strong>{image.name}</strong></p>
            <p>{t('imagePreview.size')} <strong>{ Math.round(image.size / 1024)}</strong> KB</p>
        </div>
    );

}
