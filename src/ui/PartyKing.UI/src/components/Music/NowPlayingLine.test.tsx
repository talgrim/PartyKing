import { render, screen } from '@testing-library/react';
import { NowPlayingLine } from './NowPlayingLine';
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

describe('NowPlayingLine', () => {
  it('renders nothing until the first answer arrives', () => {
    const { container } = render(<NowPlayingLine nowPlaying={undefined} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('shows the title and the artist as text, without any art', () => {
    const { container } = render(<NowPlayingLine nowPlaying={{ song, paused: false, playerConnected: true }} />);

    expect(screen.getByText('Track name')).toBeVisible();
    expect(screen.getByText('The Artist')).toBeVisible();
    expect(container.querySelector('img')).toBeNull();
    expect(screen.queryByText('Paused')).not.toBeInTheDocument();
  });

  it('shows only the video name of a YouTube song', () => {
    const youtube = { ...song, source: SongSource.Youtube, artists: null };
    render(<NowPlayingLine nowPlaying={{ song: youtube, paused: false, playerConnected: true }} />);

    expect(screen.getByText('Track name')).toBeVisible();
    expect(screen.queryByText('The Artist')).not.toBeInTheDocument();
  });

  it('says when the song is paused or the host has no player running', () => {
    render(<NowPlayingLine nowPlaying={{ song, paused: true, playerConnected: false }} />);

    expect(screen.getByText('Paused')).toBeVisible();
    expect(screen.getByText("The host's player is not connected.")).toBeVisible();
  });

  it('asks for a song when nothing plays', () => {
    render(<NowPlayingLine nowPlaying={{ song: null, paused: false, playerConnected: true }} />);

    expect(screen.getByText('Nothing is playing. Add a song to the queue.')).toBeVisible();
  });
});
