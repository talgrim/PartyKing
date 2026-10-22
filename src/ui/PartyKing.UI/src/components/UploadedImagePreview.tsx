import { ReactNode } from "react"

type ImagePreviewType = {
    image: File,
    className?: string
}

export const UploadedImagePreview = ({ image, className }: ImagePreviewType): ReactNode => {
    console.log(className);
    return (
        <div className={`p-4 rounded-xl bg-neutral-800 text-neutral-400 ${className}`}>
            <img src={URL.createObjectURL(image)} className="w-full h-auto mb-4" />
            <p>Name: <strong>{image.name}</strong></p>
            <p>Size: <strong>{ Math.round(image.size / 1024)}</strong> KB</p>
        </div>
    );

}