import { render, screen } from '@testing-library/react';
import { NowPlayingOverlay } from './NowPlayingOverlay';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import { SongSource } from '@/service/endpoints/queue/types';

const nowPlaying = (artists: string): NowPlayingDto => ({
  song: {
    id: 1,
    source: SongSource.Spotify,
    url: 'spotify:track:1',
    name: 'Track name',
    artists,
    albumArtUrl: 'http://art',
    durationMs: 1000,
  },
  paused: true,
  playerConnected: true,
});

describe('NowPlayingOverlay', () => {
  it('shows only the title and the artist', () => {
    render(<NowPlayingOverlay nowPlaying={nowPlaying('Artist')} />);

    expect(screen.getByText('Track name')).toBeVisible();
    expect(screen.getByText('Artist')).toBeVisible();
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(screen.queryByText('Paused')).not.toBeInTheDocument();
  });

  it('omits the artist line when the song has none', () => {
    render(<NowPlayingOverlay nowPlaying={nowPlaying('')} />);

    expect(screen.getByText('Track name')).toBeVisible();
  });

  it('renders nothing while no song is playing', () => {
    const { container } = render(<NowPlayingOverlay nowPlaying={{ song: null, paused: false, playerConnected: true }} />);

    expect(container).toBeEmptyDOMElement();
  });
});
