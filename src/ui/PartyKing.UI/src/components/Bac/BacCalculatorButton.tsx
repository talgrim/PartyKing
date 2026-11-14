import { ReactNode, useState } from 'react';
import LocalBarIcon from '@mui/icons-material/LocalBar';
import { useTranslation } from 'react-i18next';
import { headerIconClass, headerItemClass } from '@/components/Account/AccountMenu';
import { BacCalculatorDialog } from './BacCalculatorDialog';
import { helpTarget } from '@/components/Help/helpSteps';

/** The header icon that opens the blood alcohol calculator; it needs no login. */
export const BacCalculatorButton = (): ReactNode => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        {...helpTarget('bac')}
        type="button"
        aria-label={t('bac.title')}
        title={t('bac.title')}
        className={headerItemClass}
        onClick={() => setOpen(true)}
      >
        <LocalBarIcon fontSize="inherit" className={headerIconClass} />
      </button>
      <BacCalculatorDialog open={open} onClose={() => setOpen(false)} />
    </>
  );
};
