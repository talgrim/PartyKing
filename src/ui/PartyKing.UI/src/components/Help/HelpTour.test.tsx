import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ReactNode } from 'react';
import { useSlideshowOpen, SLIDESHOW_OPEN_KEY } from '@/components/Music/useSlideshowOpen';
import { HelpTour } from './HelpTour';
import { HelpStep, helpTarget } from './helpSteps';

const steps: HelpStep[] = [
  { target: 'games', text: 'games', audience: 'all' },
  { target: 'photos', text: 'photos', audience: 'all' },
  { target: 'language', text: 'language', audience: 'all' },
];

/** The page behind the tour: a drawer that the tour can open, and the elements to point at. */
const Page = ({ withPhotos = true, onClose = vi.fn(), tour = steps }: { withPhotos?: boolean; onClose?: () => void; tour?: HelpStep[] }): ReactNode => {
  const [drawerOpen] = useSlideshowOpen();

  return (
    <>
      <button {...helpTarget('games')}>Games</button>
      {withPhotos && <button {...helpTarget('photos')}>Photos</button>}
      <div {...helpTarget('language')}>Language</div>
      {drawerOpen && <aside {...helpTarget('slideshow')}>Drawer</aside>}
      <HelpTour steps={tour} onClose={onClose} />
    </>
  );
};

const title = (name: string) => screen.findByRole('heading', { name });

describe('HelpTour', () => {
  beforeEach(() => localStorage.clear());

  it('explains the first step next to its element, and walks forwards and back', async () => {
    render(<Page />);

    expect(await title('Games')).toBeVisible();
    expect(screen.getByText('1 of 3')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Back' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(await title('Upload photos')).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'Back' }));
    expect(await title('Games')).toBeVisible();
  });

  it('ends with Done on the last step', async () => {
    const onClose = vi.fn();
    render(<Page onClose={onClose} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Next' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Next' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Done' }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('is closed by the X and by Escape, but not by a click beside the callout', async () => {
    const onClose = vi.fn();
    render(<Page onClose={onClose} />);
    await title('Games');

    await userEvent.click(document.body);
    expect(onClose).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(onClose).toHaveBeenCalledTimes(1);

    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it('skips a step whose element is not on the page', async () => {
    render(<Page withPhotos={false} />);
    await title('Games');

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));

    expect(await screen.findByRole('heading', { name: 'Language' }, { timeout: 3000 })).toBeVisible();
    expect(screen.getByText('3 of 3')).toBeVisible();
  });

  it('keeps going back past a skipped step instead of turning around', async () => {
    render(<Page withPhotos={false} />);
    await title('Games');
    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await screen.findByRole('heading', { name: 'Language' }, { timeout: 3000 });

    await userEvent.click(screen.getByRole('button', { name: 'Back' }));

    expect(await screen.findByRole('heading', { name: 'Games' }, { timeout: 3000 })).toBeVisible();
  });

  it('ends when no step is left to show', async () => {
    const onClose = vi.fn();
    render(<Page onClose={onClose} tour={[{ target: 'qr', text: 'qr', audience: 'host' }]} />);

    await waitFor(() => expect(onClose).toHaveBeenCalled(), { timeout: 3000 });
  });

  describe('the drawer', () => {
    const drawerSteps: HelpStep[] = [
      { target: 'slideshow', text: 'slideshow', audience: 'all', needsDrawer: true },
      { target: 'language', text: 'language', audience: 'all' },
    ];

    it('is opened for a step inside it and hidden again once the tour moves on', async () => {
      localStorage.setItem(SLIDESHOW_OPEN_KEY, 'false');
      render(<Page tour={drawerSteps} />);

      expect(await title('Slideshow')).toBeVisible();
      expect(screen.getByText('Drawer')).toBeInTheDocument();

      await userEvent.click(screen.getByRole('button', { name: 'Next' }));

      expect(await title('Language')).toBeVisible();
      expect(screen.queryByText('Drawer')).not.toBeInTheDocument();
      expect(localStorage.getItem(SLIDESHOW_OPEN_KEY)).toBe('false');
    });

    it('is hidden again when the tour is closed on its step', async () => {
      localStorage.setItem(SLIDESHOW_OPEN_KEY, 'false');
      const { unmount } = render(<Page tour={drawerSteps} />);
      await title('Slideshow');

      unmount();

      expect(localStorage.getItem(SLIDESHOW_OPEN_KEY)).toBe('false');
    });

    it('is left open when the visitor had it open', async () => {
      localStorage.setItem(SLIDESHOW_OPEN_KEY, 'true');
      render(<Page tour={drawerSteps} />);
      await title('Slideshow');

      await userEvent.click(screen.getByRole('button', { name: 'Next' }));
      await title('Language');

      expect(screen.getByText('Drawer')).toBeInTheDocument();
      expect(localStorage.getItem(SLIDESHOW_OPEN_KEY)).toBe('true');
    });
  });
});
