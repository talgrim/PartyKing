import type { MockInstance } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { RequestSessionDeletion } from './RequestSessionDeletion';

const mocks = vi.hoisted(() => ({ requestDeletion: vi.fn(), getDemoConfig: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: { requestDeletion: mocks.requestDeletion } }));
vi.mock('@/service/endpoints/demo/demoApi', () => ({ demoApi: { getConfig: mocks.getDemoConfig } }));

const session = { id: 's1', code: 'ABC123', name: 'Party', hostName: 'H', hasHost: true };

const renderIcon = async (props: { deletionScheduledAt?: string | null; enabledDemo?: boolean } = {}) => {
  mocks.getDemoConfig.mockResolvedValue({ enabled: props.enabledDemo ?? false, serverTime: '2026-11-13T12:00:00Z' });
  const onRequested = vi.fn();
  const onFailed = vi.fn();
  render(
    <SWRConfig value={{ provider: () => new Map() }}>
      <RequestSessionDeletion
        session={{ ...session, deletionScheduledAt: props.deletionScheduledAt }}
        onRequested={onRequested}
        onFailed={onFailed}
      />
    </SWRConfig>,
  );
  await waitFor(() => expect(mocks.getDemoConfig).toHaveBeenCalled());

  return { onRequested, onFailed };
};

describe('RequestSessionDeletion', () => {
  let confirm: MockInstance<typeof window.confirm>;

  beforeEach(() => {
    mocks.requestDeletion.mockReset().mockResolvedValue(undefined);
    confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  afterEach(() => confirm.mockRestore());

  it('explains the delay and that only an admin can call it off, before asking the server anything', async () => {
    confirm.mockReturnValue(false);
    const { onRequested } = await renderIcon();

    await userEvent.click(await screen.findByRole('button', { name: 'Request deletion of this session' }));

    expect(confirm).toHaveBeenCalledWith(
      'Ask for this session to be deleted? In 7 days it will be deleted for good, with its queue, games and photos. Only an admin can call that off.',
    );
    expect(mocks.requestDeletion).not.toHaveBeenCalled();
    expect(onRequested).not.toHaveBeenCalled();
  });

  it('asks the server once confirmed, and tells the page so it can show the date', async () => {
    const { onRequested } = await renderIcon();

    await userEvent.click(await screen.findByRole('button', { name: 'Request deletion of this session' }));

    await waitFor(() => expect(onRequested).toHaveBeenCalledTimes(1));
    expect(mocks.requestDeletion).toHaveBeenCalledWith('s1');
  });

  it('reports why the request failed, and stays available', async () => {
    mocks.requestDeletion.mockRejectedValueOnce({ status: 429, message: 'Too many requests' });
    const { onFailed, onRequested } = await renderIcon();

    await userEvent.click(await screen.findByRole('button', { name: 'Request deletion of this session' }));

    await waitFor(() => expect(onFailed).toHaveBeenCalledWith('Too many requests'));
    expect(onRequested).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Request deletion of this session' })).toBeEnabled();
  });

  it('falls back to its own message when the error has none', async () => {
    mocks.requestDeletion.mockRejectedValueOnce({});
    const { onFailed } = await renderIcon();

    await userEvent.click(await screen.findByRole('button', { name: 'Request deletion of this session' }));

    await waitFor(() => expect(onFailed).toHaveBeenCalledWith('Could not request the deletion'));
  });

  it('is gone once a deletion is scheduled, because nothing a guest does can move it', async () => {
    await renderIcon({ deletionScheduledAt: '2026-11-28T14:05:00Z' });

    expect(screen.queryByRole('button', { name: 'Request deletion of this session' })).not.toBeInTheDocument();
  });

  it('is not offered in the demo, whose sessions end on their own', async () => {
    await renderIcon({ enabledDemo: true });

    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Request deletion of this session' })).not.toBeInTheDocument(),
    );
  });
});
