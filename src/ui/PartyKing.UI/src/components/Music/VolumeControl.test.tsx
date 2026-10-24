import { render, screen } from '@testing-library/react';
import { VolumeControl } from './VolumeControl';

describe('VolumeControl', () => {
  it('shows the volume as a percentage', () => {
    render(<VolumeControl volume={0.5} onChange={vi.fn()} />);

    expect(screen.getByText('50%')).toBeInTheDocument();
  });

  it('raises the volume by 10%', () => {
    const onChange = vi.fn();
    render(<VolumeControl volume={0.5} onChange={onChange} />);

    screen.getByRole('button', { name: 'Volume up' }).click();

    expect(onChange).toHaveBeenCalledWith(0.6);
  });

  it('lowers the volume by 10%', () => {
    const onChange = vi.fn();
    render(<VolumeControl volume={0.5} onChange={onChange} />);

    screen.getByRole('button', { name: 'Volume down' }).click();

    expect(onChange).toHaveBeenCalledWith(0.4);
  });

  it('does not drift when the steps do not add up exactly', () => {
    const onChange = vi.fn();
    render(<VolumeControl volume={0.7} onChange={onChange} />);

    screen.getByRole('button', { name: 'Volume up' }).click();

    expect(onChange).toHaveBeenCalledWith(0.8);
  });

  it('cannot go above 100%', () => {
    render(<VolumeControl volume={1} onChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Volume up' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Volume down' })).toBeEnabled();
  });

  it('cannot go below 0%', () => {
    render(<VolumeControl volume={0} onChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Volume down' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Volume up' })).toBeEnabled();
  });
});
