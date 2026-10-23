import { render, screen } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { Slideshow } from './Slideshow';

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
      <MemoryRouter>
        <Slideshow />
      </MemoryRouter>
    </SWRConfig>,
  );

describe('Slideshow', () => {
  it('shows an empty state when the API has no photo (204 arrives as an empty string)', async () => {
    mocks.fetchFromApi.mockResolvedValue('');

    renderPage();

    expect(await screen.findByText('No photos yet')).toBeInTheDocument();
  });

  it('shows an error when the photo cannot be loaded', async () => {
    mocks.fetchFromApi.mockRejectedValue(new Error('boom'));

    renderPage();

    expect(await screen.findByText('Could not load the slideshow.')).toBeInTheDocument();
  });

  it('shows the current photo', async () => {
    mocks.fetchFromApi.mockResolvedValue({ fileName: 'SampleImages/Uploaded/a.jpg', expirationDate: new Date() });

    renderPage();

    const image = await screen.findByRole('img');
    expect(image).toHaveAttribute('src', expect.stringContaining('SampleImages/Uploaded/a.jpg'));
  });
});
