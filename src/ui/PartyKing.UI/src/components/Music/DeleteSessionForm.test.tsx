import type { MockInstance } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DeleteSessionForm } from './DeleteSessionForm';

const mocks = vi.hoisted(() => ({ deleteSession: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { deleteSession: mocks.deleteSession } }));

const renderForm = (onDeleted = vi.fn()) => {
  render(<DeleteSessionForm sessionId="s1" sessionName="Party" onDeleted={onDeleted} />);
  return onDeleted;
};

describe('DeleteSessionForm', () => {
  let confirm: MockInstance<typeof window.confirm>;

  beforeEach(() => {
    mocks.deleteSession.mockReset().mockResolvedValue(undefined);
    confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  afterEach(() => confirm.mockRestore());

  it('asks for a confirmation that names the session, and deletes nothing when it is refused', async () => {
    confirm.mockReturnValue(false);
    const onDeleted = renderForm();

    await userEvent.click(screen.getByRole('button', { name: 'Delete session' }));

    expect(confirm).toHaveBeenCalledWith(
      'Delete “Party” together with its queue, games and photos? This cannot be undone.',
    );
    expect(mocks.deleteSession).not.toHaveBeenCalled();
    expect(onDeleted).not.toHaveBeenCalled();
  });

  it('deletes the session once confirmed, and tells the page', async () => {
    const onDeleted = renderForm();

    await userEvent.click(screen.getByRole('button', { name: 'Delete session' }));

    await waitFor(() => expect(onDeleted).toHaveBeenCalledTimes(1));
    expect(mocks.deleteSession).toHaveBeenCalledWith('s1');
  });

  it('shows why it failed, keeps the page, and lets the host try again', async () => {
    mocks.deleteSession.mockRejectedValueOnce({ status: 403, message: 'Only the host or an admin can change the session settings' });
    const onDeleted = renderForm();

    await userEvent.click(screen.getByRole('button', { name: 'Delete session' }));

    expect(await screen.findByText('Only the host or an admin can change the session settings')).toBeVisible();
    expect(onDeleted).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Delete session' })).toBeEnabled();
  });

  it('falls back to a general message when the error has none', async () => {
    mocks.deleteSession.mockRejectedValueOnce({});
    renderForm();

    await userEvent.click(screen.getByRole('button', { name: 'Delete session' }));

    expect(await screen.findByText('Something went wrong')).toBeVisible();
  });
});
