import { render, screen, waitFor, within } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { SiteBar } from './SiteBar';
import { setLanguage } from '@/i18n';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';

const mocks = vi.hoisted(() => ({ getDemoConfig: vi.fn(), me: vi.fn() }));

vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/demo/demoApi', () => ({ demoApi: { getConfig: mocks.getDemoConfig } }));

const renderAt = (path: string) =>
  render(
    <SWRConfig value={{ provider: () => new Map() }}>
      <MemoryRouter initialEntries={[path]}>
        <SiteBar />
      </MemoryRouter>
    </SWRConfig>,
  );

const signIn = (role: 'Admin' | 'User' = 'User') => {
  localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
  mocks.me.mockResolvedValue({ id: 'a1', username: 'boss', role, createdAt: '2026-11-09T12:00:00Z' });
};

const position = (first: HTMLElement, second: HTMLElement) =>
  first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING;

describe('SiteBar', () => {
  beforeEach(() => {
    localStorage.clear();
    mocks.me.mockReset();
    mocks.getDemoConfig.mockResolvedValue({ enabled: false, serverTime: '2026-11-13T12:00:00Z' });
  });

  describe('the title', () => {
    it('shows PartyKing before talgrim.dev, each linking to its site', () => {
      renderAt('/');

      const site = screen.getByRole('link', { name: 'PartyKing' });
      const brand = screen.getByRole('link', { name: 'talgrim.dev' });

      expect(site).toHaveAttribute('href', '/');
      expect(brand).toHaveAttribute('href', 'https://talgrim.dev/');
      expect(position(site, brand)).toBeTruthy();
    });

    it('links talgrim.dev to the Polish home page when the page is Polish', async () => {
      await setLanguage('pl');
      renderAt('/');

      expect(screen.getByRole('link', { name: 'talgrim.dev' })).toHaveAttribute('href', 'https://talgrim.dev/pl/');
    });
  });

  describe('the other sites', () => {
    it('are listed to a signed-in visitor, with this site marked as the current page', async () => {
      signIn();
      renderAt('/');

      const sites = within(await screen.findByRole('navigation', { name: 'Sites' }));

      expect(sites.getAllByRole('link').map((link) => [link.textContent, link.getAttribute('href')])).toEqual([
        ['Home', 'https://talgrim.dev/'],
        ['PartyKing', 'https://party.talgrim.dev/'],
        ['Notes', 'https://notes.talgrim.dev/'],
      ]);
      expect(sites.getByRole('link', { name: 'PartyKing' })).toHaveAttribute('aria-current', 'page');
      expect(sites.getByRole('link', { name: 'Notes' })).not.toHaveAttribute('aria-current');
    });

    it('are named in Polish when the page is', async () => {
      signIn();
      await setLanguage('pl');
      renderAt('/');

      const sites = within(await screen.findByRole('navigation', { name: 'Serwisy' }));

      expect(sites.getByRole('link', { name: 'Strona główna' })).toHaveAttribute('href', 'https://talgrim.dev/pl/');
      expect(sites.getByRole('link', { name: 'Notatki' })).toBeInTheDocument();
    });

    it('are not listed to a visitor who is not signed in, and nobody is asked who is signed in', () => {
      renderAt('/');

      expect(screen.queryByRole('navigation', { name: 'Sites' })).not.toBeInTheDocument();
      expect(mocks.me).not.toHaveBeenCalled();
    });

    it('are not listed once the login has expired', async () => {
      localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
      mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });
      renderAt('/');

      await waitFor(() => expect(mocks.me).toHaveBeenCalled());
      expect(screen.queryByRole('navigation', { name: 'Sites' })).not.toBeInTheDocument();
    });
  });

  describe('the links of this site', () => {
    it.each(['/admin', '/s/abc', '/s/abc/games/ringo'])('lead to all the sessions on %s', (path) => {
      renderAt(path);

      expect(screen.getByRole('link', { name: 'All sessions' })).toHaveAttribute('href', '/');
    });

    it('do not lead to all the sessions on the landing page, which is that list', () => {
      renderAt('/');

      expect(screen.queryByRole('link', { name: 'All sessions' })).not.toBeInTheDocument();
    });

    it.each(['/', '/admin', '/s/abc'])('have the BAC calculator on %s, for a visitor', (path) => {
      renderAt(path);

      expect(screen.getByRole('button', { name: 'BAC calculator' })).toBeInTheDocument();
    });

    it('lead to the admin panel for a signed-in admin, after the calculator, on every page', async () => {
      signIn('Admin');
      renderAt('/s/abc');

      const link = await screen.findByRole('link', { name: 'Admin panel' });

      expect(link).toHaveAttribute('href', '/admin');
      expect(position(screen.getByRole('button', { name: 'BAC calculator' }), link)).toBeTruthy();
    });

    it('do not lead to the admin panel for a signed-in user', async () => {
      signIn('User');
      renderAt('/');

      await screen.findByRole('button', { name: 'boss' });
      expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
    });

    it('keep the order: all the sessions, the calculator', () => {
      renderAt('/admin');

      expect(
        position(
          screen.getByRole('link', { name: 'All sessions' }),
          screen.getByRole('button', { name: 'BAC calculator' }),
        ),
      ).toBeTruthy();
    });
  });

  describe('the right-hand side', () => {
    it('has the language and then the account button', () => {
      renderAt('/');

      expect(
        position(screen.getByRole('group', { name: 'Language' }), screen.getByRole('link', { name: 'Log in' })),
      ).toBeTruthy();
    });

    it('offers a visitor one button, to log in', () => {
      renderAt('/');

      expect(screen.getAllByRole('link', { name: 'Log in' })).toHaveLength(1);
      expect(screen.queryByRole('link', { name: 'Sign up' })).not.toBeInTheDocument();
    });

    it('names the account on the button of a signed-in visitor', async () => {
      signIn();
      renderAt('/');

      expect(await screen.findByRole('button', { name: 'boss' })).toBeInTheDocument();
    });
  });

  it('keeps only the calculator and the language in a demo, which has no accounts, no list of sessions and no other sites', async () => {
    signIn('Admin');
    mocks.getDemoConfig.mockResolvedValue({ enabled: true, serverTime: '2026-11-13T12:00:00Z' });
    renderAt('/s/abc');

    await waitFor(() => expect(screen.queryByRole('link', { name: 'Log in' })).not.toBeInTheDocument());
    expect(screen.queryByRole('link', { name: 'All sessions' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Admin panel' })).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Sites' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'BAC calculator' })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Language' })).toBeInTheDocument();
  });
});
