import { render, screen } from '@testing-library/react';
import { PhotoDownload } from './PhotoDownload';

const mocks = vi.hoisted(() => ({
  getPhotoCount: vi.fn(),
}));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({
  sessionsApi: {
    getPhotoCount: mocks.getPhotoCount,
    photoArchiveUrl: (id: string) => `/api/sessions/${id}/photos/archive`,
  },
}));

describe('PhotoDownload', () => {
  beforeEach(() => {
    mocks.getPhotoCount.mockReset();
  });

  it('links to the archive when there are photos', async () => {
    mocks.getPhotoCount.mockResolvedValue({ count: 3 });

    render(<PhotoDownload sessionId="s1" />);

    const link = await screen.findByRole('link', { name: 'Download as ZIP' });
    expect(link).toHaveAttribute('href', '/api/sessions/s1/photos/archive');
    expect(link).toHaveAttribute('aria-disabled', 'false');
    expect(screen.getByText('3 photos are stored for this session.')).toBeInTheDocument();
  });

  it('disables the link while there are no photos', async () => {
    mocks.getPhotoCount.mockResolvedValue({ count: 0 });

    render(<PhotoDownload sessionId="s1" />);

    const link = await screen.findByRole('link', { name: 'Download as ZIP' });
    expect(link).not.toHaveAttribute('href');
    expect(link).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByText('No photos have been uploaded yet.')).toBeInTheDocument();
  });

  it('shows the error when the count cannot be read', async () => {
    mocks.getPhotoCount.mockRejectedValue(new Error('Forbidden'));

    render(<PhotoDownload sessionId="s1" />);

    expect(await screen.findByText('Forbidden')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});
