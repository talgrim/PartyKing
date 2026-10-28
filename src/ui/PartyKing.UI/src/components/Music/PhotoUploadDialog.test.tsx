import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PhotoUploadDialog } from './PhotoUploadDialog';

const mocks = vi.hoisted(() => ({ upload: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { uploadPhotos: mocks.upload } }));

const photo = (name = 'photo.png') => new File(['x'], name, { type: 'image/png' });

const renderDialog = (props: Partial<React.ComponentProps<typeof PhotoUploadDialog>> = {}) => {
  const onClose = vi.fn();
  const onUnauthorized = vi.fn();
  render(<PhotoUploadDialog sessionId="s1" open onClose={onClose} onUnauthorized={onUnauthorized} {...props} />);
  return { onClose, onUnauthorized, user: userEvent.setup() };
};

describe('PhotoUploadDialog', () => {
  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => 'blob:preview');
    URL.revokeObjectURL = vi.fn();
    mocks.upload.mockReset().mockResolvedValue(undefined);
  });

  it('renders nothing while closed', () => {
    renderDialog({ open: false });

    expect(screen.queryByText('Upload photos')).not.toBeInTheDocument();
  });

  it('offers the upload only once photos are chosen, then uploads them', async () => {
    const { user } = renderDialog();
    expect(screen.queryByRole('button', { name: /^Upload \d|^Upload 1/ })).not.toBeInTheDocument();

    await user.upload(screen.getByLabelText('Choose photos'), [photo('a.png'), photo('b.png')]);
    await user.click(screen.getByLabelText('Delete each photo after it has been shown'));
    await user.click(screen.getByRole('button', { name: 'Upload 2 photos' }));

    await waitFor(() => expect(mocks.upload).toHaveBeenCalledTimes(1));
    expect(mocks.upload.mock.calls[0][0]).toBe('s1');
    expect(mocks.upload.mock.calls[0][1]).toHaveLength(2);
    expect(mocks.upload.mock.calls[0][2]).toBe(true);
    expect(await screen.findByText('Photos uploaded')).toBeVisible();
    expect(screen.queryByRole('button', { name: /^Upload \d/ })).not.toBeInTheDocument();
  });

  it('shows the reason when the upload is refused', async () => {
    mocks.upload.mockRejectedValue({ status: 400, message: 'Unsupported file type' });
    const { user } = renderDialog();

    await user.upload(screen.getByLabelText('Choose photos'), photo());
    await user.click(screen.getByRole('button', { name: 'Upload 1 photo' }));

    expect(await screen.findByText('Unsupported file type')).toBeVisible();
  });

  it('tells the page when the session password changed meanwhile', async () => {
    mocks.upload.mockRejectedValue({ status: 401 });
    const { user, onUnauthorized } = renderDialog();

    await user.upload(screen.getByLabelText('Choose photos'), photo());
    await user.click(screen.getByRole('button', { name: 'Upload 1 photo' }));

    await waitFor(() => expect(onUnauthorized).toHaveBeenCalled());
  });

  it('closes with the X', async () => {
    const { user, onClose } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Close' }));

    expect(onClose).toHaveBeenCalled();
  });

  it('can not be closed while an upload is running', async () => {
    mocks.upload.mockReturnValue(new Promise(() => undefined));
    const { user, onClose } = renderDialog();

    await user.upload(screen.getByLabelText('Choose photos'), photo());
    await user.click(screen.getByRole('button', { name: 'Upload 1 photo' }));

    expect(await screen.findByRole('progressbar')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Close' })).toBeDisabled();
    await user.keyboard('{Escape}');
    expect(onClose).not.toHaveBeenCalled();
  });
});
