import { render, screen } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { SlideshowView } from './SlideshowView';
import { slideshowKey } from '@/service/realtime/sessionMusicEvents';

const mocks = vi.hoisted(() => ({ fetchFromApi: vi.fn() }));

vi.mock('@/service/fetcher/apiService', () => ({ fetchFromApi: mocks.fetchFromApi }));
vi.mock('@splidejs/react-splide', () => ({
  Splide: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  SplideSlide: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));
vi.mock('@splidejs/react-splide/css', () => ({}));

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter initialEntries={['/s/session-1']}>
        <Routes>
          <Route path="/s/:sessionId" element={<SlideshowView sessionId="session-1" />} />
        </Routes>
      </MemoryRouter>
    </SWRConfig>,
  );

describe('SlideshowView', () => {
  it('shows an empty state when the API has no photo (204 arrives as an empty string)', async () => {
    mocks.fetchFromApi.mockResolvedValue('');

    renderPage();

    expect(await screen.findByText('No photos yet')).toBeInTheDocument();
  });

  it('keeps the slideshow key stable so a pushed slide change refreshes the same cache entry', () => {
    expect(slideshowKey('session-1')).toEqual({ endpoint: '/sessions/session-1/photos/current' });
  });

  it('asks the session photos of the route\'s session', async () => {
    mocks.fetchFromApi.mockResolvedValue('');

    renderPage();

    await screen.findByText('No photos yet');
    expect(mocks.fetchFromApi).toHaveBeenCalledWith({ endpoint: '/sessions/session-1/photos/current' });
  });

  it('asks for the session password when the session is locked', async () => {
    mocks.fetchFromApi.mockRejectedValue({ status: 401 });

    renderPage();

    expect(await screen.findByLabelText('Session password')).toBeInTheDocument();
  });

  it('shows an error when the photo cannot be loaded', async () => {
    mocks.fetchFromApi.mockRejectedValue(new Error('boom'));

    renderPage();

    expect(await screen.findByText('Could not load the slideshow.')).toBeInTheDocument();
  });

  it('shows the current photo', async () => {
    mocks.fetchFromApi.mockResolvedValue({ fileName: 'Images/Uploaded/session-1/a.jpg', expirationDate: new Date() });

    renderPage();

    const image = await screen.findByRole('img');
    expect(image).toHaveAttribute('src', expect.stringContaining('Images/Uploaded/session-1/a.jpg'));
  });
});
