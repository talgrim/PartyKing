import { render, screen } from '@testing-library/react';
import { SessionHeader } from './SessionHeader';

vi.mock('@/service/endpoints/sessions/sessionsApi', () => ({ sessionsApi: {} }));

const session = { id: 's', code: 'ABC123', name: 'Party', hostName: 'Host Person', hasHost: true };

describe('SessionHeader', () => {
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
});
