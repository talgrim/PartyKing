import { render, screen } from '@testing-library/react';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { AppHeader } from './AppHeader';

vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: vi.fn() } }));

const renderAt = (path: string) =>
  render(
    <SWRConfig value={{ provider: () => new Map() }}>
      <MemoryRouter initialEntries={[path]}>
        <AppHeader />
      </MemoryRouter>
    </SWRConfig>,
  );

describe('AppHeader', () => {
  beforeEach(() => localStorage.clear());

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
});
