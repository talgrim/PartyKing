import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { SessionHeader } from './SessionHeader';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';

const mocks = vi.hoisted(() => ({ me: vi.fn(), getDemoConfig: vi.fn() }));

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: {} }));
vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/demo/demoApi', () => ({ demoApi: { getConfig: mocks.getDemoConfig } }));
vi.mock('./SaveQueueAsPlaylist', () => ({ SaveQueueAsPlaylist: () => <button>Save as playlist</button> }));

const session = { id: 's', code: 'ABC123', name: 'Party', hostName: 'Host Person', hasHost: true };

describe('SessionHeader', () => {
  beforeEach(() => {
    mocks.getDemoConfig.mockResolvedValue({ enabled: false, serverTime: '2026-11-13T12:00:00Z' });
  });

  describe('in the demo', () => {
    const renderDemo = async (props: { hasHost: boolean; isHost: boolean }) => {
      mocks.getDemoConfig.mockResolvedValue({ enabled: true, serverTime: '2026-11-13T12:00:00Z' });
      render(
        <SWRConfig value={{ provider: () => new Map() }}>
          <SessionHeader session={{ ...session, hasHost: props.hasHost }} isHost={props.isHost} />
        </SWRConfig>,
      );
      // The buttons are there until the page knows it is a demo
      await waitFor(() => expect(mocks.getDemoConfig).toHaveBeenCalled());
    };

    it('offers a guest no Spotify host login', async () => {
      await renderDemo({ hasHost: true, isHost: false });

      await waitFor(() => expect(screen.queryByRole('button', { name: "I'm the host" })).not.toBeInTheDocument());
      expect(screen.getByText('Current Host: Host Person')).toBeVisible();
    });

    it('offers no Spotify host login to a session without a host either', async () => {
      await renderDemo({ hasHost: false, isHost: false });

      await waitFor(() =>
        expect(screen.queryByRole('button', { name: 'Log in as host with Spotify' })).not.toBeInTheDocument(),
      );
    });
  });

  it('offers the photo upload icon to everybody once the page can open the dialog', async () => {
    const onUploadPhotos = vi.fn();
    render(<SessionHeader session={session} isHost={false} onUploadPhotos={onUploadPhotos} />);

    await userEvent.click(screen.getByRole('button', { name: 'Upload photos' }));

    expect(onUploadPhotos).toHaveBeenCalledTimes(1);
  });

  it('has no photo upload icon without a handler', () => {
    render(<SessionHeader session={session} isHost={false} />);

    expect(screen.queryByRole('button', { name: 'Upload photos' })).not.toBeInTheDocument();
  });

  it('lets a visitor of a hosted session log in again as the host', () => {
    render(<SessionHeader session={session} isHost={false} />);

    expect(screen.getByText('Current Host: Host Person')).toBeVisible();
    expect(screen.getByRole('button', { name: "I'm the host" })).toBeVisible();
  });

  it('does not offer the login to the host who is already logged in', () => {
    render(<SessionHeader session={session} isHost />);

    expect(screen.queryByRole('button', { name: "I'm the host" })).not.toBeInTheDocument();
  });

  it('offers to become the host when the session has none', () => {
    render(<SessionHeader session={{ ...session, hostName: null, hasHost: false }} isHost={false} />);

    expect(screen.getByRole('button', { name: 'Log in as host with Spotify' })).toBeVisible();
  });

  it.each([
    ['the host', { isHost: true, isAdmin: false }],
    ['an admin', { isHost: false, isAdmin: true }],
  ])('shows the settings gear to %s', (_, role) => {
    render(<SessionHeader session={session} {...role} />);

    expect(screen.getByRole('button', { name: 'Session settings' })).toBeVisible();
  });

  it('hides the settings gear from everyone else', () => {
    render(<SessionHeader session={session} isHost={false} />);

    expect(screen.queryByRole('button', { name: 'Session settings' })).not.toBeInTheDocument();
  });

  describe('the save-as-playlist button', () => {
    const renderHeader = (role: { isHost: boolean; isAdmin?: boolean }) =>
      render(
        <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
          <SessionHeader session={session} {...role} />
        </SWRConfig>,
      );

    beforeEach(() => {
      localStorage.clear();
      mocks.me.mockReset();
    });

    it.each([
      ['a guest', { isHost: false }],
      ['the host', { isHost: true }],
      ['an admin', { isHost: false, isAdmin: true }],
    ])('is shown to %s who is signed in', async (_, role) => {
      localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
      mocks.me.mockResolvedValue({ id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' });

      renderHeader(role);

      expect(await screen.findByRole('button', { name: 'Save as playlist' })).toBeVisible();
    });

    it.each([
      ['a guest', { isHost: false }],
      ['the host', { isHost: true }],
    ])('is hidden from %s who is not signed in', (_, role) => {
      renderHeader(role);

      expect(screen.queryByRole('button', { name: 'Save as playlist' })).not.toBeInTheDocument();
      expect(mocks.me).not.toHaveBeenCalled();
    });

    it('is hidden once the login has expired', async () => {
      localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
      mocks.me.mockRejectedValue({ status: 401 });

      renderHeader({ isHost: false });

      await vi.waitFor(() => expect(mocks.me).toHaveBeenCalled());
      expect(screen.queryByRole('button', { name: 'Save as playlist' })).not.toBeInTheDocument();
    });
  });
});
