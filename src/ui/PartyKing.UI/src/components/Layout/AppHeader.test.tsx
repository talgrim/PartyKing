import { render, screen, waitFor } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { AppHeader } from './AppHeader';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';

const mocks = vi.hoisted(() => ({ getDemoConfig: vi.fn(), me: vi.fn() }));

vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/demo/demoApi', () => ({ demoApi: { getConfig: mocks.getDemoConfig } }));

const renderAt = (path: string) =>
  render(
    <SWRConfig value={{ provider: () => new Map() }}>
      <MemoryRouter initialEntries={[path]}>
        <AppHeader />
      </MemoryRouter>
    </SWRConfig>,
  );

const position = (first: HTMLElement, second: HTMLElement) => first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING;

describe('AppHeader', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.me.mockReset();
    mocks.getDemoConfig.mockResolvedValue({ enabled: false, serverTime: '2026-11-13T12:00:00Z' });
  });

  it.each(['/admin', '/s/abc', '/s/abc/games/ringo'])('links to all the sessions on %s, next to the account button', (path) => {
    renderAt(path);

    expect(screen.getByRole('link', { name: 'All sessions' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();
  });

  it('has no link to all the sessions on the landing page, which is that list', () => {
    renderAt('/');

    expect(screen.queryByRole('link', { name: 'All sessions' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();
  });

  it('shows the link as an icon, named and titled "All sessions"', () => {
    renderAt('/admin');

    const link = screen.getByRole('link', { name: 'All sessions' });

    expect(link).toHaveAttribute('title', 'All sessions');
    expect(link).toHaveTextContent('');
    expect(link.querySelector('svg')).toBeInTheDocument();
  });

  it('puts the link before the account button', () => {
    renderAt('/admin');

    const link = screen.getByRole('link', { name: 'All sessions' });
    const button = screen.getByRole('button', { name: 'Log in' });

    expect(link.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it.each(['/', '/admin', '/s/abc'])('has the BAC calculator on %s, for a visitor', (path) => {
    renderAt(path);

    expect(screen.getByRole('button', { name: 'BAC calculator' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument();
  });

  it('keeps only the calculator in a demo, which has no accounts and no list of sessions', async () => {
    mocks.getDemoConfig.mockResolvedValue({ enabled: true, serverTime: '2026-11-13T12:00:00Z' });
    renderAt('/s/abc');

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Log in' })).not.toBeInTheDocument());
    expect(screen.queryByRole('link', { name: 'All sessions' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'BAC calculator' })).toBeInTheDocument();
  });

  describe('the admin panel link', () => {
    const signIn = (role: 'Admin' | 'User') => {
      localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
      mocks.me.mockResolvedValue({ id: 'a1', username: 'boss', role, createdAt: '2026-11-09T12:00:00Z' });
    };

    it('is for a signed-in admin, between the calculator and the account button, on every page', async () => {
      signIn('Admin');
      renderAt('/s/abc');

      const link = await screen.findByRole('link', { name: 'Admin panel' });

      expect(link).toHaveAttribute('href', '/admin');
      expect(position(screen.getByRole('button', { name: 'BAC calculator' }), link)).toBeTruthy();
      expect(position(link, await screen.findByRole('button', { name: /boss/ }))).toBeTruthy();
    });

    it('is not shown to a signed-in user', async () => {
      signIn('User');
      renderAt('/');

      await waitFor(() => expect(mocks.me).toHaveBeenCalled());
      expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
    });

    it('is not shown to a visitor without an account, and nobody is asked who is signed in', () => {
      renderAt('/');

      expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
      expect(mocks.me).not.toHaveBeenCalled();
    });

    it('is not shown once the login has expired', async () => {
      localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
      mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });
      renderAt('/');

      await waitFor(() => expect(mocks.me).toHaveBeenCalled());
      expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
    });
  });

  it('puts the calculator between the link to all the sessions and the account button', () => {
    renderAt('/admin');

    const link = screen.getByRole('link', { name: 'All sessions' });
    const calculator = screen.getByRole('button', { name: 'BAC calculator' });
    const account = screen.getByRole('button', { name: 'Log in' });

    expect(position(link, calculator)).toBeTruthy();
    expect(position(calculator, account)).toBeTruthy();
  });
});
