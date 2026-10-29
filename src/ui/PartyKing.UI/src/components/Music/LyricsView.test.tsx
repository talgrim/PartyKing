import { act, fireEvent, render, screen } from '@testing-library/react';
import { LyricsView } from './LyricsView';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import { SongSource } from '@/service/endpoints/queue/types';

const lines = [
  { timeMs: 1000, text: 'first line' },
  { timeMs: 5000, text: 'second line' },
  { timeMs: 9000, text: '' },
];

const found = { status: 'found' as const, songId: 1, lines, offsetMs: 0 };

const nowPlaying = (overrides: Partial<NowPlayingDto> = {}): NowPlayingDto => ({
  song: {
    id: 1,
    source: SongSource.Spotify,
    url: 'spotify:track:1',
    name: 'Song',
    artists: 'Artist',
    albumArtUrl: null,
    durationMs: 60000,
  },
  paused: false,
  playerConnected: true,
  positionMs: 0,
  ...overrides,
});

describe('LyricsView', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it.each([
    ['loading', 'Looking for the lyrics…'],
    ['none', 'No synchronized lyrics for this song'],
    ['idle', 'Nothing is playing'],
  ] as const)('says so while the lyrics are %s', (status, message) => {
    render(<LyricsView lyrics={{ status }} nowPlaying={nowPlaying()} />);

    expect(screen.getByText(message)).toBeInTheDocument();
  });

  it('highlights the line being sung at the reported position', () => {
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 6000 })} />);

    expect(screen.getByText('second line')).toHaveAttribute('aria-current', 'true');
    expect(screen.getByText('first line')).not.toHaveAttribute('aria-current');
  });

  it('moves to the next line as the song plays on', () => {
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 2000 })} />);
    expect(screen.getByText('first line')).toHaveAttribute('aria-current', 'true');

    act(() => {
      vi.advanceTimersByTime(4000);
    });

    expect(screen.getByText('second line')).toHaveAttribute('aria-current', 'true');
  });

  it('stays on the line while the song is paused', () => {
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 2000, paused: true })} />);

    act(() => {
      vi.advanceTimersByTime(10000);
    });

    expect(screen.getByText('first line')).toHaveAttribute('aria-current', 'true');
  });

  it('shows a note for an empty line', () => {
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 9500 })} />);

    expect(screen.getByText('♪')).toHaveAttribute('aria-current', 'true');
  });

  it('shifts the lines with the timing buttons', () => {
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 4600, paused: true })} />);
    expect(screen.getByText('first line')).toHaveAttribute('aria-current', 'true');

    fireEvent.click(screen.getByRole('button', { name: 'Show lyrics earlier' }));

    expect(screen.getByText('second line')).toHaveAttribute('aria-current', 'true');
    expect(screen.getByRole('button', { name: /Lyrics timing \+0\.5 s/ })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Lyrics timing/ }));

    expect(screen.getByText('first line')).toHaveAttribute('aria-current', 'true');
  });

  it('changes the text size with the magnifier buttons, within limits, and remembers it', () => {
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 2000 })} />);
    const line = () => screen.getByText('second line');
    expect(line()).toHaveStyle({ fontSize: '1.25rem' });

    fireEvent.click(screen.getByRole('button', { name: 'Larger lyrics' }));

    expect(line()).toHaveStyle({ fontSize: '1.5rem' });
    expect(localStorage.getItem('partyking.lyricsFontScale')).toBe('120');

    fireEvent.click(screen.getByRole('button', { name: 'Smaller lyrics' }));
    fireEvent.click(screen.getByRole('button', { name: 'Smaller lyrics' }));
    fireEvent.click(screen.getByRole('button', { name: 'Smaller lyrics' }));

    expect(line()).toHaveStyle({ fontSize: '0.75rem' });
    expect(screen.getByRole('button', { name: 'Smaller lyrics' })).toBeDisabled();
  });

  it('asks for the position again and catches up when the window gets focus back', () => {
    const onResync = vi.fn();
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 2000 })} onResync={onResync} />);

    fireEvent.focus(window);

    expect(onResync).toHaveBeenCalledTimes(1);
  });

  it('resyncs on the button', () => {
    const onResync = vi.fn();
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying()} onResync={onResync} />);

    fireEvent.click(screen.getByRole('button', { name: 'Resync lyrics' }));

    expect(onResync).toHaveBeenCalledTimes(1);
  });

  describe('timing offset shared through the server', () => {
    const timing = () => screen.getByRole('button', { name: /^Lyrics timing/ });

    it('starts at the offset saved for the song', () => {
      render(<LyricsView lyrics={{ ...found, offsetMs: -1500 }} nowPlaying={nowPlaying()} />);

      expect(timing()).toHaveTextContent('−1.5 s');
    });

    it('starts the next song at its own saved offset, dropping what was nudged', () => {
      const { rerender } = render(<LyricsView lyrics={found} nowPlaying={nowPlaying()} />);
      fireEvent.click(screen.getByRole('button', { name: 'Show lyrics earlier' }));
      expect(timing()).toHaveTextContent('+0.5 s');

      rerender(<LyricsView lyrics={{ ...found, songId: 2 }} nowPlaying={nowPlaying()} />);

      expect(timing()).toHaveTextContent('0.0 s');
    });

    it('replaces a nudged value when a new offset is saved', () => {
      const { rerender } = render(<LyricsView lyrics={found} nowPlaying={nowPlaying()} />);
      fireEvent.click(screen.getByRole('button', { name: 'Show lyrics earlier' }));

      rerender(<LyricsView lyrics={{ ...found, offsetMs: 2000 }} nowPlaying={nowPlaying()} />);

      expect(timing()).toHaveTextContent('+2.0 s');
    });

    it('offers no save button to a viewer who cannot save', () => {
      render(<LyricsView lyrics={found} nowPlaying={nowPlaying()} />);

      expect(screen.queryByRole('button', { name: 'Save lyrics timing' })).not.toBeInTheDocument();
    });

    it('saves the nudged value with the tick, which is disabled until the value differs', async () => {
      const onSaveOffset = vi.fn().mockResolvedValue(undefined);
      render(<LyricsView lyrics={found} nowPlaying={nowPlaying()} onSaveOffset={onSaveOffset} />);
      const save = screen.getByRole('button', { name: 'Save lyrics timing' });
      expect(save).toBeDisabled();

      fireEvent.click(screen.getByRole('button', { name: 'Show lyrics earlier' }));
      expect(save).toBeEnabled();
      await act(async () => {
        fireEvent.click(save);
      });

      expect(onSaveOffset).toHaveBeenCalledWith(500);
    });

    it('keeps the tick available when saving fails', async () => {
      const onSaveOffset = vi.fn().mockRejectedValue(new Error('403'));
      render(<LyricsView lyrics={found} nowPlaying={nowPlaying()} onSaveOffset={onSaveOffset} />);

      fireEvent.click(screen.getByRole('button', { name: 'Show lyrics earlier' }));
      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: 'Save lyrics timing' }));
      });

      expect(screen.getByRole('button', { name: 'Save lyrics timing' })).toBeEnabled();
    });
  });

  it('opens on the line being sung and jumps there at once, as after moving between drawer and maximized view', () => {
    const scrollTo = vi.fn();
    Element.prototype.scrollTo = scrollTo;

    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 6000 })} />);

    expect(screen.getByText('second line')).toHaveAttribute('aria-current', 'true');
    expect(scrollTo).toHaveBeenCalledTimes(1);
    expect(scrollTo).toHaveBeenCalledWith(expect.objectContaining({ behavior: 'auto' }));
  });
});
