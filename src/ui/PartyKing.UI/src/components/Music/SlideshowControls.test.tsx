import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SlideshowControls } from './SlideshowControls';

const renderControls = (props: Partial<Parameters<typeof SlideshowControls>[0]> = {}) => {
  const onToggleLyrics = vi.fn();
  render(
    <SlideshowControls
      open
      onToggle={vi.fn()}
      onMaximize={vi.fn()}
      lyricsActive={false}
      lyricsAvailable
      onToggleLyrics={onToggleLyrics}
      {...props}
    />,
  );
  return { onToggleLyrics };
};

describe('SlideshowControls lyrics button', () => {
  it('offers the lyrics while the slideshow is shown', () => {
    renderControls();

    expect(screen.getByRole('button', { name: 'Show lyrics' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('offers the slideshow while the lyrics are shown', () => {
    renderControls({ lyricsActive: true });

    expect(screen.getByRole('button', { name: 'Show slideshow' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('stays usable for a song without lyrics, with a crossed-out icon and a hint', async () => {
    const withLyrics = renderControls();
    const iconWith = screen.getByRole('button', { name: 'Show lyrics' }).innerHTML;
    document.body.innerHTML = '';

    const { onToggleLyrics } = renderControls({ lyricsAvailable: false });
    const button = screen.getByRole('button', { name: 'Show lyrics' });

    expect(button).toBeEnabled();
    expect(button).toHaveAttribute('title', 'Show lyrics (none found for this song)');
    expect(button.innerHTML).not.toBe(iconWith);

    await userEvent.click(button);
    expect(onToggleLyrics).toHaveBeenCalledTimes(1);
    expect(withLyrics.onToggleLyrics).not.toHaveBeenCalled();
  });

  it('shows a different icon for switching back to the slideshow, also for a song without lyrics', () => {
    renderControls();
    const lyricsIcon = screen.getByRole('button', { name: 'Show lyrics' }).innerHTML;
    document.body.innerHTML = '';

    renderControls({ lyricsActive: true, lyricsAvailable: false });
    const button = screen.getByRole('button', { name: 'Show slideshow' });

    expect(button.innerHTML).not.toBe(lyricsIcon);
    expect(button).not.toHaveClass('text-slate-500');
  });
});
