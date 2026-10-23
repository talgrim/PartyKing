import { render, screen } from '@testing-library/react';
import { NowPlayingCard } from './NowPlayingCard';
import { SongSource } from '@/service/endpoints/queue/types';

const song = {
  id: 1,
  source: SongSource.Spotify,
  url: 'spotify:track:1',
  name: 'Track name',
  artists: 'The Artist',
  albumArtUrl: 'https://i.scdn.co/image/art',
  durationMs: 1000,
};

describe('NowPlayingCard', () => {
  it('renders nothing until the first answer arrives', () => {
    const { container } = render(<NowPlayingCard nowPlaying={undefined} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('shows the title, the artist and the album art of the song', () => {
    const { container } = render(
      <NowPlayingCard nowPlaying={{ song, paused: false, playerConnected: true }} />,
    );

    expect(screen.getByText('Track name')).toBeVisible();
    expect(screen.getByText('The Artist')).toBeVisible();
    expect(container.querySelector('img')).toHaveAttribute('src', 'https://i.scdn.co/image/art');
    expect(screen.queryByText('Paused')).not.toBeInTheDocument();
  });

  it('works for a YouTube song that has no artist and no thumbnail', () => {
    const youtube = { ...song, source: SongSource.Youtube, artists: null, albumArtUrl: null };
    const { container } = render(
      <NowPlayingCard nowPlaying={{ song: youtube, paused: false, playerConnected: true }} />,
    );

    expect(screen.getByText('Track name')).toBeVisible();
    expect(container.querySelector('img')).toBeNull();
  });

  it('says so when the song is paused', () => {
    render(<NowPlayingCard nowPlaying={{ song, paused: true, playerConnected: true }} />);

    expect(screen.getByText('Paused')).toBeVisible();
  });

  it('says so when no player is connected', () => {
    render(<NowPlayingCard nowPlaying={{ song, paused: false, playerConnected: false }} />);

    expect(screen.getByText("The host's player is not connected.")).toBeVisible();
  });

  it('says so when nothing is playing', () => {
    render(<NowPlayingCard nowPlaying={{ song: null, paused: false, playerConnected: false }} />);

    expect(screen.getByText(/Nothing is playing/)).toBeVisible();
  });
});
