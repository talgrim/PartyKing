import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BacCalculatorButton } from './BacCalculatorButton';

describe('BacCalculatorButton', () => {
  beforeEach(() => localStorage.clear());

  it('is an icon named and titled "BAC calculator"', () => {
    render(<BacCalculatorButton />);

    const button = screen.getByRole('button', { name: 'BAC calculator' });
    expect(button).toHaveAttribute('title', 'BAC calculator');
    expect(button.querySelector('svg')).toBeInTheDocument();
  });

  it('opens the calculator and closes it again', async () => {
    const user = userEvent.setup();
    render(<BacCalculatorButton />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'BAC calculator' }));
    expect(await screen.findByRole('dialog', { name: 'BAC calculator' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
