import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PlayPauseButton } from './PlayPauseButton';

describe('PlayPauseButton', () => {
  it('offers to pause while playing', () => {
    render(<PlayPauseButton paused={false} onToggle={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Pause' })).toBeInTheDocument();
  });

  it('offers to play while paused', () => {
    render(<PlayPauseButton paused onToggle={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Play' })).toBeInTheDocument();
  });

  it('calls onToggle when clicked', async () => {
    const onToggle = vi.fn();
    render(<PlayPauseButton paused={false} onToggle={onToggle} />);

    await userEvent.click(screen.getByRole('button'));

    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('does not call onToggle when disabled', async () => {
    const onToggle = vi.fn();
    render(<PlayPauseButton paused={false} disabled onToggle={onToggle} />);

    await userEvent.click(screen.getByRole('button'));

    expect(onToggle).not.toHaveBeenCalled();
  });
});
