import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PageNavigation } from './PageNavigation';

describe('PageNavigation', () => {
  it('renders a link for a destination and a button for an action', async () => {
    const onClick = vi.fn();
    render(
      <MemoryRouter>
        <PageNavigation links={[{ to: '/', label: 'All sessions' }, { onClick, label: 'Upload photos' }]} />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'All sessions' })).toHaveAttribute('href', '/');
    await userEvent.click(screen.getByRole('button', { name: 'Upload photos' }));
    expect(onClick).toHaveBeenCalled();
  });
});
