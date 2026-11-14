import HelpOutlineIcon from '@mui/icons-material/HelpOutline';
import { ReactNode, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useIsDemo } from '@/service/endpoints/demo/useDemoConfig';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { HelpTour } from './HelpTour';
import { stepsFor } from './helpSteps';

export const HELP_SEEN_KEY = 'partyking.helpSeen';

type Props = {
  /** The visitor hosts the session (or is an admin): the tour also explains the player and the settings. */
  canManage: boolean;
};

/**
 * The "?" in the session header of a demo, which starts a tour of the page for someone who has not seen the app. Its
 * border turns until the first click, after which it is an icon like the others. It is not there outside a demo.
 */
export const HelpButton = ({ canManage }: Props): ReactNode => {
  const { t } = useTranslation();
  const isDemo = useIsDemo();
  const [seen, setSeen] = usePersistedFlag(HELP_SEEN_KEY, false);
  const [open, setOpen] = useState(false);
  const steps = useMemo(() => stepsFor(canManage), [canManage]);

  if (!isDemo) return null;

  return (
    <>
      <button
        type="button"
        onClick={() => {
          setSeen(true);
          setOpen(true);
        }}
        aria-label={t('help.open')}
        title={t('help.open')}
        aria-haspopup="dialog"
        className={`flex h-8 w-8 items-center justify-center cursor-pointer ${
          seen ? 'text-slate-400 hover:text-slate-200' : 'help-attention rounded-full text-amber-400'
        }`}
      >
        <HelpOutlineIcon fontSize={seen ? 'medium' : 'small'} />
      </button>
      {open && <HelpTour steps={steps} onClose={() => setOpen(false)} />}
    </>
  );
};
