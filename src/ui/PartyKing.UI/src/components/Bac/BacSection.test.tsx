import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BacSection } from './BacSection';

const renderSection = (props: Partial<Parameters<typeof BacSection>[0]> = {}) => {
  const onToggle = vi.fn();
  render(
    <BacSection title="Part" expanded={false} onToggle={onToggle} {...props}>
      <input aria-label="inside" />
    </BacSection>,
  );
  return onToggle;
};

describe('BacSection', () => {
  it('shows its content when it is expanded and hides it when it is not', () => {
    const { unmount } = render(
      <BacSection title="Part" expanded onToggle={vi.fn()}>
        <input aria-label="inside" />
      </BacSection>,
    );
    expect(screen.getByRole('button', { name: 'Part' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByLabelText('inside')).toBeVisible();
    unmount();

    renderSection();

    expect(screen.getByRole('button', { name: 'Part' })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByLabelText('inside')).not.toBeVisible();
  });

  it('asks to toggle when its title is clicked', async () => {
    const user = userEvent.setup();
    const onToggle = renderSection();

    await user.click(screen.getByRole('button', { name: 'Part' }));

    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('cannot be opened when disabled, even if it was asked to be expanded', async () => {
    const user = userEvent.setup();
    const onToggle = renderSection({ expanded: true, disabled: true });

    await user.click(screen.getByRole('button', { name: 'Part' }));

    expect(screen.getByRole('button', { name: 'Part' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Part' })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByLabelText('inside')).not.toBeVisible();
    expect(onToggle).not.toHaveBeenCalled();
  });

  it('describes the title with its note', () => {
    renderSection({ note: 'Some summary' });

    expect(screen.getByRole('button', { name: 'Part' })).toHaveAccessibleDescription('Some summary');
  });

  it('has the arrow in its header, which is one button holding the title and the note', async () => {
    const user = userEvent.setup();
    const onToggle = renderSection({ note: 'Some summary' });
    const header = screen.getByRole('button', { name: 'Part' });

    expect(header.querySelector('svg')).toBeInTheDocument();
    expect(header).toHaveClass('w-full');
    await user.click(screen.getByText('Some summary'));

    expect(onToggle).toHaveBeenCalledTimes(1);
  });
});
