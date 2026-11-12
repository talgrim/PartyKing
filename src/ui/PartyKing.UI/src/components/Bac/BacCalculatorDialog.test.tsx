import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { setLanguage } from '@/i18n';
import { BAC_STORAGE_KEY } from './bacStorage';
import { BacCalculatorDialog } from './BacCalculatorDialog';

const renderOpen = () => render(<BacCalculatorDialog open onClose={vi.fn()} />);

const fillWeight = async (user: ReturnType<typeof userEvent.setup>, kg: string) => {
  await user.type(screen.getByLabelText('Weight (kg)'), kg);
};

describe('BacCalculatorDialog', () => {
  beforeEach(() => localStorage.clear());

  it('warns that it is only an estimate', () => {
    renderOpen();

    expect(screen.getByText(/Never use it to decide whether you can drive/)).toBeInTheDocument();
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

  it('adds a drink, which is not absorbed yet, and removes it', async () => {
    const user = userEvent.setup();
    renderOpen();
    await fillWeight(user, '80');

    await user.click(screen.getByRole('button', { name: '+ Beer' }));

    expect(screen.getByLabelText('Beer: Volume (ml)')).toHaveValue(500);
    expect(screen.getByLabelText('Beer: Minutes ago')).toHaveValue(0);

    await user.click(screen.getByRole('button', { name: 'Remove Beer' }));

    expect(screen.getByText('No drinks yet. Add what you drank.')).toBeInTheDocument();
  });

  it('raises the estimate for a drink started an hour ago', async () => {
    const user = userEvent.setup();
    renderOpen();
    await fillWeight(user, '80');
    await user.click(screen.getByRole('button', { name: '+ Beer' }));

    const minutes = screen.getByLabelText('Beer: Minutes ago');
    await user.clear(minutes);
    await user.type(minutes, '60');

    const result = screen.getByRole('region', { name: 'Estimate' });
    const now = result.querySelector('p[aria-live]');
    expect(now).not.toHaveTextContent('0.000 %');
    expect(now).toHaveTextContent(/^0\.[1-9]\d* %|^0\.0[1-9]\d* %|^0\.00[1-9] %/);
  });

  it('switches the time of a drink to a clock with the switch', async () => {
    const user = userEvent.setup();
    renderOpen();
    await user.click(screen.getByRole('button', { name: '+ Wine' }));

    await user.click(screen.getByRole('checkbox', { name: 'Pick the exact time' }));

    expect(screen.queryByLabelText('Wine: Minutes ago')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Wine: Started at')).toHaveAttribute('type', 'datetime-local');
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
    await user.click(screen.getByRole('button', { name: '+ Shot' }));
    unmount();

    renderOpen();

    expect(screen.getByLabelText('Weight (kg)')).toHaveValue(70);
    expect(screen.getByRole('radio', { name: 'Female' })).toBeChecked();
    expect(screen.getByLabelText('Shot: Volume (ml)')).toHaveValue(40);
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
    await user.click(screen.getByRole('button', { name: '+ Beer' }));
    await user.click(screen.getByRole('button', { name: '+ Wine' }));

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
