import { act, fireEvent, render, screen } from '@testing-library/react';
import { LyricsView } from './LyricsView';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import { SongSource } from '@/service/endpoints/queue/types';

const lines = [
  { timeMs: 1000, text: 'first line' },
  { timeMs: 5000, text: 'second line' },
  { timeMs: 9000, text: '' },
];

const found = { status: 'found' as const, songId: 1, lines };

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

  it('shifts the lines with the timing buttons and remembers the choice', () => {
    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 4600, paused: true })} />);
    expect(screen.getByText('first line')).toHaveAttribute('aria-current', 'true');

    fireEvent.click(screen.getByRole('button', { name: 'Show lyrics earlier' }));

    expect(screen.getByText('second line')).toHaveAttribute('aria-current', 'true');
    expect(localStorage.getItem('partyking.lyricsOffsetMs')).toBe('500');
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

  it('opens on the line being sung and jumps there at once, as after moving between drawer and maximized view', () => {
    const scrollTo = vi.fn();
    Element.prototype.scrollTo = scrollTo;

    render(<LyricsView lyrics={found} nowPlaying={nowPlaying({ positionMs: 6000 })} />);

    expect(screen.getByText('second line')).toHaveAttribute('aria-current', 'true');
    expect(scrollTo).toHaveBeenCalledTimes(1);
    expect(scrollTo).toHaveBeenCalledWith(expect.objectContaining({ behavior: 'auto' }));
  });
});
