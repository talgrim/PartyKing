import { ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { totalGramsOfAlcohol } from './bacCalculation';
import { WEIGHT_RANGE, BacState, inRange } from './bacStorage';
import { BacDrinkList } from './BacDrinkList';
import { BacProfileForm } from './BacProfileForm';
import { BacSection } from './BacSection';

type Props = {
  state: BacState;
  update: (patch: Partial<BacState>) => void;
  now: number;
};

/**
 * The two collapsible sections of the calculator. What they start as is decided when the dialog opens (its content is
 * mounted again each time): "About you" is open until there is a weight to compute with and shut once there is (its header shows no data), and
 * "Drinks" is the other way round. Drinks cannot be opened without a weight, because there is nothing to estimate from.
 */
export const BacSections = ({ state, update, now }: Props): ReactNode => {
  const { t, i18n } = useTranslation();
  const hasWeight = inRange(state.weightKg, WEIGHT_RANGE);
  const [profileOpen, setProfileOpen] = useState(!hasWeight);
  const [drinksOpen, setDrinksOpen] = useState(hasWeight);

  const grams = totalGramsOfAlcohol(state.drinks).toLocaleString(i18n.language, { maximumFractionDigits: 1 });
  const drinksNote = !hasWeight ? t('bac.drinksLocked') : state.drinks.length > 0 ? t('bac.alcoholGrams', { grams }) : undefined;

  return (
    <>
      <BacSection
        title={t('bac.profile')}
        expanded={profileOpen}
        onToggle={() => setProfileOpen((open) => !open)}
      >
        <BacProfileForm state={state} update={update} />
      </BacSection>
      <BacSection
        title={t('bac.drinks')}
        expanded={drinksOpen}
        onToggle={() => setDrinksOpen((open) => !open)}
        disabled={!hasWeight}
        note={drinksNote}
      >
        <BacDrinkList state={state} update={update} now={now} />
      </BacSection>
    </>
  );
};
