import { ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BacCalculatorDialog } from './BacCalculatorDialog';
import { helpTarget } from '@/components/Help/helpSteps';

/** The link-like button in the site bar that opens the blood alcohol calculator; it needs no login. */
export const BacCalculatorButton = (): ReactNode => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <>
      <button {...helpTarget('bac')} type="button" onClick={() => setOpen(true)}>
        {t('bac.title')}
      </button>
      <BacCalculatorDialog open={open} onClose={() => setOpen(false)} />
    </>
  );
};
