import { ReactNode, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import CloseIcon from '@mui/icons-material/Close';
import { Dialog, DialogContent, DialogTitle, useMediaQuery } from '@mui/material';
import { useNow } from '@/utils/useNow';
import { BacDrink, BacProfile, estimateBac, usesTotalBodyWater } from './bacCalculation';
import { WEIGHT_RANGE, inRange, AGE_RANGE, HEIGHT_RANGE, VOLUME_RANGE, ABV_RANGE } from './bacStorage';
import { bacUnitFor } from './bacUnits';
import { BacDrinkList } from './BacDrinkList';
import { BacProfileForm } from './BacProfileForm';
import { BacResult } from './BacResult';
import { useBacState } from './useBacState';

type Props = {
  open: boolean;
  onClose: () => void;
};

const PHONE_QUERY = '(max-width: 639px)';
const TICK_MS = 30_000;

/**
 * The blood alcohol calculator, open to everybody, a visitor included. It estimates from the profile and the drinks the
 * browser remembers; nothing is sent anywhere. Full screen on a phone.
 */
export const BacCalculatorDialog = ({ open, onClose }: Props): ReactNode => {
  const { t, i18n } = useTranslation();
  const fullScreen = useMediaQuery(PHONE_QUERY);
  const now = useNow(TICK_MS);
  const [state, update] = useBacState();

  const profile: BacProfile | null = useMemo(() => {
    if (!inRange(state.weightKg, WEIGHT_RANGE)) return null;
    return {
      weightKg: state.weightKg,
      sex: state.sex,
      heightCm: inRange(state.heightCm, HEIGHT_RANGE) ? state.heightCm : null,
      age: inRange(state.age, AGE_RANGE) ? state.age : null,
    };
  }, [state.weightKg, state.sex, state.heightCm, state.age]);

  const estimate = useMemo(() => {
    if (!profile) return null;
    const drinks: BacDrink[] = state.drinks.filter(
      (drink) => inRange(drink.volumeMl, VOLUME_RANGE) && inRange(drink.abvPercent, ABV_RANGE),
    );
    return estimateBac(profile, drinks, now);
  }, [profile, state.drinks, now]);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      aria-labelledby="bac-title"
      fullWidth
      fullScreen={fullScreen}
      maxWidth="sm"
      // MUI's emotion styles are unlayered and beat Tailwind's layered utilities, hence the important modifier
      slotProps={{ paper: { className: 'bg-slate-800! text-slate-200! rounded-xl!' } }}
    >
      <DialogTitle id="bac-title" className="flex! items-center justify-between text-xl! font-semibold!">
        {t('bac.title')}
        <button
          type="button"
          onClick={onClose}
          aria-label={t('common.close')}
          title={t('common.close')}
          className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
        >
          <CloseIcon />
        </button>
      </DialogTitle>
      <DialogContent className="space-y-5">
        <p className="text-sm text-amber-500">{t('bac.disclaimer')}</p>
        <BacProfileForm state={state} update={update} />
        <BacDrinkList state={state} update={update} now={now} />
        {profile && estimate ? (
          <BacResult
            estimate={estimate}
            unit={bacUnitFor(i18n.language)}
            now={now}
            totalBodyWater={usesTotalBodyWater(profile)}
          />
        ) : (
          <p className="text-sm text-slate-400">{t('bac.needWeight')}</p>
        )}
      </DialogContent>
    </Dialog>
  );
};
