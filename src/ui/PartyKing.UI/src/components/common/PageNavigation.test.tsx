import { render, screen, waitFor } from '@testing-library/react';
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

  it('drops a menu of actions from a button and closes it after one is chosen', async () => {
    const settings = vi.fn();
    const logOut = vi.fn();
    render(
      <MemoryRouter>
        <PageNavigation
          links={[
            {
              label: 'ann',
              menu: [
                { label: 'Account settings', onClick: settings },
                { label: 'Log out', onClick: logOut },
              ],
            },
          ]}
        />
      </MemoryRouter>,
    );

    expect(screen.queryByRole('menuitem')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'ann' }));
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Account settings', 'Log out']);

    await userEvent.click(screen.getByRole('menuitem', { name: 'Log out' }));

    expect(logOut).toHaveBeenCalledTimes(1);
    expect(settings).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.queryByRole('menuitem')).not.toBeInTheDocument());
  });
});
