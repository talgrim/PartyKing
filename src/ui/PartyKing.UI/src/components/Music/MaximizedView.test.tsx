import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MaximizedView } from './MaximizedView';

vi.mock('@/pages/Slideshow/SlideshowView', () => ({ SlideshowView: () => <div>slideshow</div> }));
vi.mock('@/components/Music/QueueList', () => ({ QueueList: () => <div>queue list</div> }));
vi.mock('@/components/Music/LyricsView', () => ({ LyricsView: () => <div>lyrics</div> }));

const renderView = () =>
  render(
    <MaximizedView
      sessionId="session"
      nowPlaying={undefined}
      showLyrics={false}
      lyrics={{ lines: [], isLoading: false } as never}
      onExit={vi.fn()}
    />,
  );

describe('MaximizedView', () => {
  beforeEach(() => localStorage.clear());

  it('shows the queue by default', () => {
    renderView();

    expect(screen.getByRole('complementary', { name: 'Queue' })).toBeVisible();
  });

  it('hides and shows the queue with the toggle and remembers the choice', async () => {
    const user = userEvent.setup();
    const { unmount } = renderView();

    await user.click(screen.getByRole('button', { name: 'Hide queue' }));
    expect(screen.queryByRole('complementary', { name: 'Queue' })).not.toBeInTheDocument();

    unmount();
    renderView();
    expect(screen.queryByRole('complementary', { name: 'Queue' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Show queue' }));
    expect(screen.getByRole('complementary', { name: 'Queue' })).toBeVisible();
  });
});
