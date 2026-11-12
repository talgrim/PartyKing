import { useState } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WheelNumberInput } from './WheelNumberInput';

const Field = ({ start = 10, min, max, step }: { start?: number | null; min?: number; max?: number; step?: number }) => {
  const [value, setValue] = useState<number | null>(start);
  return <WheelNumberInput aria-label="amount" value={value} onChange={setValue} min={min} max={max} step={step} />;
};

const scroll = (deltaY: number) => fireEvent.wheel(screen.getByLabelText('amount'), { deltaY });

describe('WheelNumberInput', () => {
  it('adds a step when scrolled up and takes one off when scrolled down, while it has the focus', () => {
    render(<Field step={5} />);
    screen.getByLabelText('amount').focus();

    scroll(-100);
    expect(screen.getByLabelText('amount')).toHaveValue(15);

    scroll(100);
    scroll(100);
    expect(screen.getByLabelText('amount')).toHaveValue(5);
  });

  it('leaves the value alone, and the scrolling to the page, when it has no focus', () => {
    render(<Field />);

    const notPrevented = scroll(-100);

    expect(screen.getByLabelText('amount')).toHaveValue(10);
    expect(notPrevented).toBe(true);
  });

  it('keeps the page from scrolling while it changes the value', () => {
    render(<Field />);
    screen.getByLabelText('amount').focus();

    expect(scroll(-100)).toBe(false);
  });

  it('stays within its range', () => {
    render(<Field start={99} min={0} max={100} step={5} />);
    screen.getByLabelText('amount').focus();

    scroll(-100);
    scroll(-100);

    expect(screen.getByLabelText('amount')).toHaveValue(100);
  });

  it('steps by fractions without rounding noise', () => {
    render(<Field start={4.5} step={0.5} />);
    screen.getByLabelText('amount').focus();

    scroll(-100);

    expect(screen.getByLabelText('amount')).toHaveValue(5);
  });

  it('is empty without a value, and still takes typing', async () => {
    const user = userEvent.setup();
    render(<Field start={null} />);

    expect(screen.getByLabelText('amount')).toHaveValue(null);
    await user.type(screen.getByLabelText('amount'), '42');

    expect(screen.getByLabelText('amount')).toHaveValue(42);
  });
});
