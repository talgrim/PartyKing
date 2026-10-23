import { render, screen, within } from '@testing-library/react';
import { ErrorLog, ErrorLogEntry } from './ErrorLog';

const entry = (id: number, message: string): ErrorLogEntry => ({
  id,
  time: new Date(2026, 0, 1, 12, 0, id),
  message,
});

describe('ErrorLog', () => {
  it('says so when there are no errors', () => {
    render(<ErrorLog entries={[]} />);

    expect(screen.getByRole('region', { name: 'Error log' })).toHaveTextContent('No errors');
  });

  it('lists the newest error first', () => {
    render(<ErrorLog entries={[entry(0, 'first problem'), entry(1, 'second problem')]} />);

    const items = within(screen.getByRole('list')).getAllByRole('listitem');

    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent('second problem');
    expect(items[1]).toHaveTextContent('first problem');
  });
});
