import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MenuButton } from './MenuButton';

describe('MenuButton', () => {
  it('puts the icon to the right of the label, and the button is still named by the label', () => {
    render(<MenuButton label="ann" icon={<svg data-testid="icon" />} entries={[]} />);

    const button = screen.getByRole('button', { name: 'ann' });

    expect(button.lastElementChild).toBe(screen.getByTestId('icon'));
    expect(button.firstElementChild).toHaveTextContent('ann');
  });

  it('drops a menu of actions from the button and closes it after one is chosen', async () => {
    const settings = vi.fn();
    const logOut = vi.fn();
    render(
      <MenuButton
        label="ann"
        entries={[
          { label: 'Account settings', onClick: settings },
          { label: 'Log out', onClick: logOut },
        ]}
      />,
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
