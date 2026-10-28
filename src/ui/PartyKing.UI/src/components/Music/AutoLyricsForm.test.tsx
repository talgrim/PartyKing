import { render, renderHook, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { act } from 'react';
import { AutoLyricsForm } from './AutoLyricsForm';
import { useAutoLyrics } from './useAutoLyrics';

describe('AutoLyricsForm', () => {
  beforeEach(() => localStorage.clear());

  it('is off by default', () => {
    render(<AutoLyricsForm />);

    expect(screen.getByRole('checkbox')).not.toBeChecked();
  });

  it('turns the automatic switching on and remembers it', async () => {
    render(<AutoLyricsForm />);

    await userEvent.click(screen.getByRole('checkbox'));

    expect(screen.getByRole('checkbox')).toBeChecked();
    expect(localStorage.getItem('partyking.autoLyrics')).toBe('true');
  });

  it('tells other users of the setting at once', async () => {
    const { result } = renderHook(() => useAutoLyrics());
    render(<AutoLyricsForm />);

    await userEvent.click(screen.getByRole('checkbox'));

    expect(result.current[0]).toBe(true);
    act(() => result.current[1](false));
    expect(screen.getByRole('checkbox')).not.toBeChecked();
  });
});
