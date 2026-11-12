import { ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { Popover } from '@mui/material';

/** The "i" next to the close button: it opens the warning about how far the estimate can be trusted. */
export const BacInfoButton = (): ReactNode => {
  const { t } = useTranslation();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  return (
    <>
      <button
        type="button"
        onClick={(e) => setAnchor(e.currentTarget)}
        aria-label={t('bac.about')}
        title={t('bac.about')}
        aria-haspopup="dialog"
        className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
      >
        <InfoOutlinedIcon />
      </button>
      <Popover
        open={anchor !== null}
        anchorEl={anchor}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { className: 'bg-slate-700! text-slate-200! max-w-xs rounded-lg!' } }}
      >
        <p className="p-3 text-sm text-amber-400">{t('bac.disclaimer')}</p>
      </Popover>
    </>
  );
};
