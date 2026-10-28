import MusicNoteIcon from '@mui/icons-material/MusicNote';
import { ReactNode } from 'react';

type Props = {
  imageUrl: string | null | undefined;
  size?: 'md' | 'lg';
};

/** Cover of a playlist or a track, or a music icon when it has none. */
export const CoverImage = ({ imageUrl, size = 'md' }: Props): ReactNode => {
  const box = size === 'lg' ? 'size-14' : 'size-10';

  return imageUrl ? (
    <img src={imageUrl} alt="" className={`${box} shrink-0 rounded-md object-cover`} />
  ) : (
    <span className={`${box} shrink-0 flex items-center justify-center rounded-md bg-slate-700 text-slate-400`}>
      <MusicNoteIcon />
    </span>
  );
};
