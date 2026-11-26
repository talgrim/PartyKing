import { render, screen } from '@testing-library/react';
import { YoutubePlayer } from './YoutubePlayer';

type PlayerProps = { src?: string; volume?: number; onError?: (event: { target: unknown }) => void };

vi.mock('react-player', () => ({
  default: ({ src, volume, onError }: PlayerProps) => (
    <>
      <span>{`src ${src}`}</span>
      <span>{`volume ${volume}`}</span>
      <button onClick={() => onError?.({ target: { error: { code: 150 } } })}>fail</button>
      <button onClick={() => onError?.({ target: {} })}>fail without code</button>
    </>
  ),
}));

describe('YoutubePlayer', () => {
  it('reports the YouTube error code', () => {
    const onError = vi.fn();
    render(<YoutubePlayer songUrl="https://youtu.be/x" mute={false} onError={onError} />);

    screen.getByText('fail').click();

    expect(onError).toHaveBeenCalledWith(150);
  });

  it('reports an error without a code', () => {
    const onError = vi.fn();
    render(<YoutubePlayer songUrl="https://youtu.be/x" mute={false} onError={onError} />);

    screen.getByText('fail without code').click();

    expect(onError).toHaveBeenCalledWith(undefined);
  });

  it('plays the video from the privacy-enhanced domain', () => {
    render(<YoutubePlayer songUrl="https://www.youtube.com/watch?v=dQw4w9WgXcQ" mute={false} />);

    expect(screen.getByText('src https://www.youtube-nocookie.com/watch?v=dQw4w9WgXcQ')).toBeInTheDocument();
  });

  it('plays at 50% by default', () => {
    render(<YoutubePlayer songUrl="https://youtu.be/x" mute={false} />);

    expect(screen.getByText('volume 0.5')).toBeInTheDocument();
  });

  it('plays at the given volume', () => {
    render(<YoutubePlayer songUrl="https://youtu.be/x" mute={false} volume={0.8} />);

    expect(screen.getByText('volume 0.8')).toBeInTheDocument();
  });
});
