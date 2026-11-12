import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { setLanguage } from '@/i18n';
import { BAC_STORAGE_KEY } from './bacStorage';
import { BacCalculatorDialog } from './BacCalculatorDialog';

const renderOpen = () => render(<BacCalculatorDialog open onClose={vi.fn()} />);

type User = ReturnType<typeof userEvent.setup>;

const fillWeight = async (user: User, kg: string) => {
  await user.type(screen.getByLabelText('Weight (kg)'), kg);
};

/** The drinks section starts shut until the dialog is opened again with a weight saved, so a test opens it by hand. */
const openDrinks = async (user: User) => {
  await user.click(screen.getByRole('button', { name: 'Drinks' }));
};

const saveDrinks = (drinks: { id: string; kind: string; volumeMl: number; abvPercent: number }[]) =>
  localStorage.setItem(
    BAC_STORAGE_KEY,
    JSON.stringify({ version: 1, weightKg: 80, sex: 'male', drinks: drinks.map((drink) => ({ ...drink, startedAt: Date.now() })) }),
  );

const saveWeight = (kg: number) => localStorage.setItem(BAC_STORAGE_KEY, JSON.stringify({ version: 1, weightKg: kg, sex: 'male' }));

describe('BacCalculatorDialog', () => {
  beforeEach(() => localStorage.clear());

  it('keeps the warning that it is only an estimate behind the "i" next to the close button', async () => {
    const user = userEvent.setup();
    renderOpen();
    expect(screen.queryByText(/Never use it to decide whether you can drive/)).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'About the estimate' }));

    expect(await screen.findByText(/Never use it to decide whether you can drive/)).toBeInTheDocument();
  });

  it('puts the "i" before the close button', () => {
    renderOpen();

    const info = screen.getByRole('button', { name: 'About the estimate' });
    const close = screen.getByRole('button', { name: 'Close' });

    expect(info.compareDocumentPosition(close) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('asks for the weight before it shows an estimate', () => {
    renderOpen();

    expect(screen.getByText('Enter your weight to see the estimate.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Estimate' })).not.toBeInTheDocument();
  });

  it('shows the estimate in percent once there is a weight', async () => {
    const user = userEvent.setup();
    renderOpen();

    await fillWeight(user, '80');

    const result = screen.getByRole('region', { name: 'Estimate' });
    expect(within(result).getByText('0.000 %')).toBeInTheDocument();
    expect(within(result).getByText(/Driving limit \(US\): 0\.080 %/)).toBeInTheDocument();
  });

  it('adds a drink to the list, which is not absorbed yet, and removes it', async () => {
    const user = userEvent.setup();
    renderOpen();
    await fillWeight(user, '80');
    await openDrinks(user);

    await user.click(screen.getByRole('button', { name: 'Add drink' }));

    expect(screen.getByText(/500 ml · 5%/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Remove Beer' }));

    expect(screen.getByText('No drinks yet. Add what you drank.')).toBeInTheDocument();
  });

  it('raises the estimate for a drink started an hour ago', async () => {
    const user = userEvent.setup();
    renderOpen();
    await fillWeight(user, '80');
    await openDrinks(user);

    const minutes = screen.getByLabelText('Minutes ago');
    await user.clear(minutes);
    await user.type(minutes, '60');
    await user.click(screen.getByRole('button', { name: 'Add drink' }));

    const result = screen.getByRole('region', { name: 'Estimate' });
    const now = result.querySelector('p[aria-live]');
    expect(now).not.toHaveTextContent('0.000 %');
    expect(now).toHaveTextContent(/^0\.[1-9]\d* %|^0\.0[1-9]\d* %|^0\.00[1-9] %/);
  });

  it('counts the same drink again from now with the icon of its entry', async () => {
    const user = userEvent.setup();
    renderOpen();
    await fillWeight(user, '80');
    await openDrinks(user);
    await user.click(screen.getByRole('button', { name: 'Add drink' }));

    await user.click(screen.getByRole('button', { name: 'Add another Beer now' }));

    expect(screen.getAllByText(/500 ml · 5%/)).toHaveLength(2);
  });

  it('changes the weight with the mouse wheel while the field is focused', async () => {
    const user = userEvent.setup();
    renderOpen();
    await fillWeight(user, '80');

    fireEvent.wheel(screen.getByLabelText('Weight (kg)'), { deltaY: -100 });

    expect(screen.getByLabelText('Weight (kg)')).toHaveValue(81);
  });

  it('says which model it used: Watson with a height and an age', async () => {
    const user = userEvent.setup();
    renderOpen();
    await fillWeight(user, '80');
    expect(screen.getByText(/average factor for your sex/)).toBeInTheDocument();

    await user.type(screen.getByLabelText('Height (cm)'), '180');
    await user.type(screen.getByLabelText('Age (years)'), '30');

    expect(screen.getByText(/total body water \(Watson\)/)).toBeInTheDocument();
  });

  it('remembers the profile and the drinks in the browser', async () => {
    const user = userEvent.setup();
    const { unmount } = renderOpen();
    await fillWeight(user, '70');
    await user.click(screen.getByRole('radio', { name: 'Female' }));
    await openDrinks(user);
    await user.click(screen.getByRole('button', { name: 'Shot' }));
    await user.click(screen.getByRole('button', { name: 'Add drink' }));
    unmount();

    renderOpen();

    expect(screen.getByText(/40 ml · 40%/)).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'About you' }));
    expect(screen.getByLabelText('Weight (kg)')).toHaveValue(70);
    expect(screen.getByRole('radio', { name: 'Female' })).toBeChecked();
    expect(localStorage.getItem(BAC_STORAGE_KEY)).toContain('"weightKg":70');
  });

  it('starts empty when what it saved is corrupt', () => {
    localStorage.setItem(BAC_STORAGE_KEY, '{oops');

    renderOpen();

    expect(screen.getByLabelText('Weight (kg)')).toHaveValue(null);
  });

  it('clears all the drinks', async () => {
    const user = userEvent.setup();
    renderOpen();
    await fillWeight(user, '80');
    await openDrinks(user);
    await user.click(screen.getByRole('button', { name: 'Add drink' }));
    await user.click(screen.getByRole('button', { name: 'Add drink' }));

    await user.click(screen.getByRole('button', { name: 'Clear all drinks' }));

    expect(screen.getByText('No drinks yet. Add what you drank.')).toBeInTheDocument();
  });

  it('shows promille and the Polish limits in Polish', async () => {
    await setLanguage('pl');
    const user = userEvent.setup();
    renderOpen();

    await user.type(screen.getByLabelText('Waga (kg)'), '80');

    expect(screen.getByText('0,00 ‰')).toBeInTheDocument();
    expect(screen.getByText(/Stan po użyciu alkoholu \(kierowca\): 0,20 ‰/)).toBeInTheDocument();
    expect(screen.getByText(/Stan nietrzeźwości: 0,50 ‰/)).toBeInTheDocument();
  });

  describe('the sections', () => {
    const state = (name: string) => screen.getByRole('button', { name }).getAttribute('aria-expanded');

    it('opens "About you" and cannot open "Drinks" while there is no weight', () => {
      renderOpen();

      expect(state('About you')).toBe('true');
      expect(state('Drinks')).toBe('false');
      expect(screen.getByRole('button', { name: 'Drinks' })).toBeDisabled();
      expect(screen.getByText('Enter your weight first')).toBeInTheDocument();
    });

    it('shuts "About you", without showing its data in the header, and opens "Drinks" when the dialog opens with a weight saved', () => {
      saveWeight(72);

      renderOpen();

      expect(state('About you')).toBe('false');
      expect(screen.getByRole('button', { name: 'About you' })).toHaveTextContent(/^About you$/);
      expect(screen.queryByText(/72 kg/)).not.toBeInTheDocument();
      expect(screen.getByLabelText('Weight (kg)')).not.toBeVisible();
      expect(state('Drinks')).toBe('true');
      expect(screen.getByRole('button', { name: 'Add drink' })).toBeVisible();
    });

    it('sums up the alcohol drunk, in grams, in the header of "Drinks"', async () => {
      const user = userEvent.setup();
      saveDrinks([
        { id: 'a', kind: 'beer', volumeMl: 500, abvPercent: 5 },
        { id: 'b', kind: 'shot', volumeMl: 40, abvPercent: 40 },
      ]);
      renderOpen();

      expect(screen.getByRole('button', { name: 'Drinks' })).toHaveAccessibleDescription('32.3 g of alcohol');

      await user.click(screen.getByRole('button', { name: 'Remove Shot' }));

      expect(screen.getByRole('button', { name: 'Drinks' })).toHaveAccessibleDescription('19.7 g of alcohol');
    });

    it('shows no grams while there are no drinks, and counts a drink as soon as it is added', async () => {
      const user = userEvent.setup();
      saveWeight(80);
      renderOpen();
      expect(screen.getByRole('button', { name: 'Drinks' })).not.toHaveAccessibleDescription();

      await user.click(screen.getByRole('button', { name: 'Add drink' }));

      expect(screen.getByRole('button', { name: 'Drinks' })).toHaveAccessibleDescription('19.7 g of alcohol');
    });

    it('says the grams in Polish with a decimal comma', async () => {
      await setLanguage('pl');
      saveDrinks([{ id: 'a', kind: 'beer', volumeMl: 500, abvPercent: 5 }]);
      renderOpen();

      expect(screen.getByRole('button', { name: 'Drinki' })).toHaveAccessibleDescription('19,7 g alkoholu');
    });

    it('lets "Drinks" be opened once the weight is filled, but does not open it by itself', async () => {
      const user = userEvent.setup();
      renderOpen();

      await fillWeight(user, '80');

      expect(screen.getByRole('button', { name: 'Drinks' })).toBeEnabled();
      expect(state('Drinks')).toBe('false');
      expect(state('About you')).toBe('true');

      await openDrinks(user);

      expect(state('Drinks')).toBe('true');
    });

    it('opens "Drinks" by default on the next opening of the dialog after the weight was filled', async () => {
      const user = userEvent.setup();
      const { unmount } = renderOpen();
      await fillWeight(user, '80');
      unmount();

      renderOpen();

      expect(state('Drinks')).toBe('true');
      expect(state('About you')).toBe('false');
    });

    it('shuts "Drinks" again when the weight is cleared', async () => {
      const user = userEvent.setup();
      renderOpen();
      await fillWeight(user, '80');
      await openDrinks(user);

      await user.clear(screen.getByLabelText('Weight (kg)'));

      expect(state('Drinks')).toBe('false');
      expect(screen.getByRole('button', { name: 'Drinks' })).toBeDisabled();
    });

    it('opens and shuts "About you" from its title', async () => {
      const user = userEvent.setup();
      saveWeight(72);
      renderOpen();

      await user.click(screen.getByRole('button', { name: 'About you' }));
      expect(screen.getByLabelText('Weight (kg)')).toBeVisible();

      await user.click(screen.getByRole('button', { name: 'About you' }));
      expect(screen.getByLabelText('Weight (kg)')).not.toBeVisible();
    });

    it('keeps what was typed in a shut section', async () => {
      const user = userEvent.setup();
      saveWeight(72);
      renderOpen();
      await user.clear(screen.getByLabelText('Volume (ml)'));
      await user.type(screen.getByLabelText('Volume (ml)'), '250');

      await user.click(screen.getByRole('button', { name: 'Drinks' }));
      await user.click(screen.getByRole('button', { name: 'Drinks' }));

      expect(screen.getByLabelText('Volume (ml)')).toHaveValue(250);
    });

    it('has the Polish names "O Tobie" and "Drinki"', async () => {
      await setLanguage('pl');
      renderOpen();

      expect(screen.getByRole('button', { name: 'O Tobie' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Drinki' })).toBeDisabled();
    });
  });

  it('works when storage is unavailable', async () => {
    const user = userEvent.setup();
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    renderOpen();

    await fillWeight(user, '80');

    expect(screen.getByRole('region', { name: 'Estimate' })).toBeInTheDocument();
    vi.restoreAllMocks();
  });
});
