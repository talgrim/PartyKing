import { render, screen } from '@testing-library/react';
import { YoutubePlayer } from './YoutubePlayer';

type PlayerProps = { onError?: (event: { target: unknown }) => void };

vi.mock('react-player', () => ({
  default: ({ onError }: PlayerProps) => (
    <>
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
});
