import { useState } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BacState, DEFAULT_BAC_STATE, StoredDrink } from './bacStorage';
import { BacDrinkList } from './BacDrinkList';

const List = ({ initial = DEFAULT_BAC_STATE }: { initial?: BacState }) => {
  const [state, setState] = useState(initial);
  return <BacDrinkList state={state} update={(patch) => setState((current) => ({ ...current, ...patch }))} now={Date.now()} />;
};

const setup = () => userEvent.setup();

const addBeer = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getByRole('button', { name: 'Add drink' }));
};

describe('BacDrinkList', () => {
  it('shows an added drink as a read-only entry with icons to edit it, delete it and add it again', async () => {
    const user = setup();
    render(<List />);

    await addBeer(user);

    expect(screen.getByText(/500 ml · 5%/)).toBeInTheDocument();
    expect(screen.getByText(/just now/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Beer: Volume (ml)')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit Beer' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove Beer' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add another Beer now' })).toBeInTheDocument();
  });

  it('shows the grams of alcohol of an entry between its strength and its time', async () => {
    const user = setup();
    render(<List />);

    await addBeer(user);

    expect(screen.getByText(/^500 ml · 5% · 19\.7 g · just now \(.+\)$/)).toBeInTheDocument();
  });

  it('works out the grams again when an entry is edited', async () => {
    const user = setup();
    render(<List />);
    await addBeer(user);

    await user.click(screen.getByRole('button', { name: 'Edit Beer' }));
    await user.clear(screen.getByLabelText('Beer: Volume (ml)'));
    await user.type(screen.getByLabelText('Beer: Volume (ml)'), '250');
    await user.click(screen.getByRole('button', { name: 'Save Beer' }));

    expect(screen.getByText(/^250 ml · 5% · 9\.9 g · /)).toBeInTheDocument();
  });

  it('fills the form with the usual serving of the kind picked, which can be changed before adding', async () => {
    const user = setup();
    render(<List />);

    await user.click(screen.getByRole('button', { name: 'Wine' }));
    expect(screen.getByLabelText('Volume (ml)')).toHaveValue(150);
    expect(screen.getByLabelText('Alcohol (%)')).toHaveValue(12);

    await user.clear(screen.getByLabelText('Volume (ml)'));
    await user.type(screen.getByLabelText('Volume (ml)'), '200');
    await addBeer(user);

    expect(screen.getByText(/Wine/, { selector: 'div' })).toBeInTheDocument();
    expect(screen.getByText(/200 ml · 12%/)).toBeInTheDocument();
  });

  it('cannot add a drink without a volume', async () => {
    const user = setup();
    render(<List />);

    await user.clear(screen.getByLabelText('Volume (ml)'));

    expect(screen.getByRole('button', { name: 'Add drink' })).toBeDisabled();
  });

  it('starts a drink the typed minutes ago', async () => {
    const user = setup();
    render(<List />);

    await user.clear(screen.getByLabelText('Minutes ago'));
    await user.type(screen.getByLabelText('Minutes ago'), '45');
    await addBeer(user);

    expect(screen.getByText(/45 min ago/)).toBeInTheDocument();
  });

  it('adds up the hours of a drink from long ago', async () => {
    const user = setup();
    render(<List />);

    await user.clear(screen.getByLabelText('Minutes ago'));
    await user.type(screen.getByLabelText('Minutes ago'), '125');
    await addBeer(user);

    expect(screen.getByText(/2 h 5 min ago/)).toBeInTheDocument();
  });

  it('edits an entry in place and saves it', async () => {
    const user = setup();
    render(<List />);
    await addBeer(user);

    await user.click(screen.getByRole('button', { name: 'Edit Beer' }));
    await user.clear(screen.getByLabelText('Beer: Volume (ml)'));
    await user.type(screen.getByLabelText('Beer: Volume (ml)'), '330');
    await user.click(screen.getByRole('button', { name: 'Save Beer' }));

    expect(screen.getByText(/330 ml · 5%/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Beer: Volume (ml)')).not.toBeInTheDocument();
  });

  it('drops the changes of an entry when the editing is cancelled', async () => {
    const user = setup();
    render(<List />);
    await addBeer(user);

    await user.click(screen.getByRole('button', { name: 'Edit Beer' }));
    await user.clear(screen.getByLabelText('Beer: Volume (ml)'));
    await user.type(screen.getByLabelText('Beer: Volume (ml)'), '330');
    await user.click(screen.getByRole('button', { name: 'Cancel editing Beer' }));

    expect(screen.getByText(/500 ml · 5%/)).toBeInTheDocument();
  });

  it('cannot save an entry whose volume was cleared', async () => {
    const user = setup();
    render(<List />);
    await addBeer(user);

    await user.click(screen.getByRole('button', { name: 'Edit Beer' }));
    await user.clear(screen.getByLabelText('Beer: Volume (ml)'));

    expect(screen.getByRole('button', { name: 'Save Beer' })).toBeDisabled();
  });

  it('adds the same drink again with the same values, started now', async () => {
    const user = setup();
    render(<List />);
    await user.clear(screen.getByLabelText('Minutes ago'));
    await user.type(screen.getByLabelText('Minutes ago'), '60');
    await addBeer(user);

    await user.click(screen.getByRole('button', { name: 'Add another Beer now' }));

    expect(screen.getAllByText(/500 ml · 5%/)).toHaveLength(2);
    expect(screen.getByText(/1 h 0 min ago/)).toBeInTheDocument();
    expect(screen.getByText(/just now/)).toBeInTheDocument();
  });

  it('deletes an entry', async () => {
    const user = setup();
    render(<List />);
    await addBeer(user);

    await user.click(screen.getByRole('button', { name: 'Remove Beer' }));

    expect(screen.getByText('No drinks yet. Add what you drank.')).toBeInTheDocument();
  });

  it('picks the time on a clock with the switch, in the form and in an entry being edited', async () => {
    const user = setup();
    render(<List />);

    await user.click(screen.getByRole('checkbox', { name: 'Pick the exact time' }));
    expect(screen.getByLabelText('Started at')).toHaveAttribute('type', 'datetime-local');
    expect(screen.queryByLabelText('Minutes ago')).not.toBeInTheDocument();

    await addBeer(user);
    await user.click(screen.getByRole('button', { name: 'Edit Beer' }));

    expect(screen.getByLabelText('Beer: Started at')).toHaveAttribute('type', 'datetime-local');
  });

  it('changes the number in a focused field with the mouse wheel', async () => {
    const user = setup();
    render(<List />);
    await user.click(screen.getByLabelText('Volume (ml)'));

    fireEvent.wheel(screen.getByLabelText('Volume (ml)'), { deltaY: -100 });

    expect(screen.getByLabelText('Volume (ml)')).toHaveValue(510);
  });

  describe('a long list', () => {
    const MINUTE = 60_000;
    const drink = (id: string, kind: StoredDrink['kind'], minutesAgo: number): StoredDrink => ({
      id,
      kind,
      volumeMl: 100,
      abvPercent: 10,
      startedAt: Date.now() - minutesAgo * MINUTE,
    });

    it('shows the drink started last on top, whatever order they were added in', () => {
      render(<List initial={{ ...DEFAULT_BAC_STATE, drinks: [drink('a', 'wine', 90), drink('b', 'shot', 5), drink('c', 'beer', 40)] }} />);

      const names = screen.getAllByRole('listitem').map((item) => item.querySelector('.font-medium')?.textContent);

      expect(names).toEqual(['Shot', 'Beer', 'Wine']);
    });

    it('puts a drink added now on top, above older ones', async () => {
      const user = setup();
      render(<List initial={{ ...DEFAULT_BAC_STATE, drinks: [drink('a', 'wine', 90)] }} />);

      await user.click(screen.getByRole('button', { name: 'Shot' }));
      await addBeer(user);

      const names = screen.getAllByRole('listitem').map((item) => item.querySelector('.font-medium')?.textContent);
      expect(names).toEqual(['Shot', 'Wine']);
    });

    it('scrolls inside a panel of limited height instead of growing with the list', () => {
      const drinks = Array.from({ length: 15 }, (_, index) => drink(String(index), 'beer', index));
      render(<List initial={{ ...DEFAULT_BAC_STATE, drinks }} />);

      const panel = screen.getByRole('list', { name: 'Drinks' });

      expect(panel).toHaveClass('overflow-y-auto');
      expect(panel.className).toMatch(/max-h-/);
      expect(screen.getAllByRole('listitem')).toHaveLength(15);
    });
  });
});
