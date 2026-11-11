import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SWRConfig } from 'swr';
import { MemoryRouter } from 'react-router-dom';
import { AdminUsers, USERS_PAGE_SIZE } from './AdminUsers';
import { setLanguage } from '@/i18n';
import { ACCOUNT_HINT_KEY } from '@/service/endpoints/account/useAccount';

const mocks = vi.hoisted(() => ({ me: vi.fn(), listUsers: vi.fn(), resetPassword: vi.fn() }));

vi.mock('@/service/endpoints/account/accountApi', () => ({ accountApi: { me: mocks.me } }));
vi.mock('@/service/endpoints/admin/adminApi', () => ({
  adminApi: { listUsers: mocks.listUsers, resetPassword: mocks.resetPassword },
}));

const admin = { id: 'a1', username: 'boss', role: 'Admin', createdAt: '2026-11-09T12:00:00Z' };
const user = { id: 'u1', username: 'ann', role: 'User', createdAt: '2026-11-09T12:00:00Z' };

const pageOf = (names: string[], page = 1, totalCount = names.length) => ({
  items: names.map((username, i) => ({ id: `id-${username}`, username, role: 'User', createdAt: `2026-11-0${i + 1}T12:00:00Z` })),
  page,
  pageSize: USERS_PAGE_SIZE,
  totalCount,
});

const renderPage = () =>
  render(
    <SWRConfig value={{ provider: () => new Map(), dedupingInterval: 0 }}>
      <MemoryRouter>
        <AdminUsers />
      </MemoryRouter>
    </SWRConfig>,
  );

describe('AdminUsers', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem(ACCOUNT_HINT_KEY, 'true');
    Object.values(mocks).forEach((mock) => mock.mockReset());
    mocks.me.mockResolvedValue(admin);
    mocks.listUsers.mockResolvedValue(pageOf(['ann', 'bob']));
  });

  it('turns a user away, and asks the API for no users', async () => {
    mocks.me.mockResolvedValue(user);

    renderPage();

    expect(await screen.findByText(/Only an admin can see the users/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to the admin page' })).toHaveAttribute('href', '/admin');
    expect(mocks.listUsers).not.toHaveBeenCalled();
  });

  it('turns a visitor away who is not signed in', async () => {
    localStorage.clear();
    mocks.me.mockRejectedValue({ status: 401, message: 'Unauthorized' });

    renderPage();

    expect(await screen.findByText(/Only an admin can see the users/)).toBeInTheDocument();
    expect(mocks.listUsers).not.toHaveBeenCalled();
  });

  it('lists the users for an admin whose browser lost the account hint', async () => {
    localStorage.clear();

    renderPage();

    expect(await screen.findByText('ann')).toBeInTheDocument();
  });

  it('lists the first page of users with their registration dates', async () => {
    renderPage();

    expect(await screen.findByText('ann')).toBeInTheDocument();
    expect(screen.getByText('bob')).toBeInTheDocument();
    expect(mocks.listUsers).toHaveBeenCalledWith({ search: '', page: 1, pageSize: USERS_PAGE_SIZE });
    expect(screen.getAllByText(/^Registered \d/)).toHaveLength(2);
    expect(screen.getByText(/Page 1 of 1/)).toBeInTheDocument();
    expect(screen.getByText(/Users: 2/)).toBeInTheDocument();
  });

  it('says so when nobody has registered', async () => {
    mocks.listUsers.mockResolvedValue(pageOf([]));

    renderPage();

    expect(await screen.findByText('Nobody has registered yet.')).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Users pages' })).not.toBeInTheDocument();
  });

  it('moves between the pages', async () => {
    mocks.listUsers.mockImplementation(({ page }: { page: number }) =>
      Promise.resolve(page === 1 ? pageOf(['ann'], 1, USERS_PAGE_SIZE + 1) : pageOf(['zed'], 2, USERS_PAGE_SIZE + 1)),
    );
    renderPage();
    await screen.findByText('ann');
    const pagination = screen.getByRole('navigation', { name: 'Users pages' });

    expect(within(pagination).getByRole('button', { name: 'Previous' })).toBeDisabled();
    await userEvent.click(within(pagination).getByRole('button', { name: 'Next' }));

    expect(await screen.findByText('zed')).toBeInTheDocument();
    expect(mocks.listUsers).toHaveBeenLastCalledWith({ search: '', page: 2, pageSize: USERS_PAGE_SIZE });
    expect(screen.getByText(/Page 2 of 2/)).toBeInTheDocument();
    expect(within(pagination).getByRole('button', { name: 'Next' })).toBeDisabled();
  });

  it('searches by username from the first page, once typing pauses', async () => {
    mocks.listUsers.mockImplementation(({ search }: { search: string }) =>
      Promise.resolve(search ? pageOf(['hannah'], 1, 1) : pageOf(['ann', 'bob'])),
    );
    renderPage();
    await screen.findByText('ann');

    await userEvent.type(screen.getByRole('searchbox', { name: 'Search users by username' }), 'han');

    expect(await screen.findByText('hannah')).toBeInTheDocument();
    await waitFor(() =>
      expect(mocks.listUsers).toHaveBeenLastCalledWith({ search: 'han', page: 1, pageSize: USERS_PAGE_SIZE }),
    );
    // One request for the pause, not one for each key
    expect(mocks.listUsers.mock.calls.filter(([params]) => params.search !== '')).toHaveLength(1);
  });

  it('says when no user matches the search', async () => {
    mocks.listUsers.mockImplementation(({ search }: { search: string }) =>
      Promise.resolve(search ? pageOf([]) : pageOf(['ann'])),
    );
    renderPage();
    await screen.findByText('ann');

    await userEvent.type(screen.getByRole('searchbox', { name: 'Search users by username' }), 'zzz');

    expect(await screen.findByText('No user matches “zzz”.')).toBeInTheDocument();
  });

  it('resets the password of the chosen user', async () => {
    mocks.resetPassword.mockResolvedValue(undefined);
    renderPage();
    await screen.findByText('ann');

    await userEvent.click(screen.getAllByRole('button', { name: 'Reset password' })[0]);
    const dialog = await screen.findByRole('dialog', { name: 'Reset the password of ann' });
    const submit = within(dialog).getByRole('button', { name: 'Reset password' });
    await userEvent.type(within(dialog).getByLabelText('New password'), 'short');
    expect(submit).toBeDisabled();
    await userEvent.type(within(dialog).getByLabelText('New password'), '-enough');
    await userEvent.click(submit);

    expect(mocks.resetPassword).toHaveBeenCalledWith('id-ann', 'short-enough');
    expect(await screen.findByRole('status')).toHaveTextContent('The password of ann was reset');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('shows why a password could not be reset, and keeps the dialog open', async () => {
    mocks.resetPassword.mockRejectedValue({ status: 404, message: 'The account was not found' });
    renderPage();
    await screen.findByText('ann');

    await userEvent.click(screen.getAllByRole('button', { name: 'Reset password' })[0]);
    const dialog = await screen.findByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText('New password'), 'brand-new-password');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reset password' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('The account was not found');
  });

  it('is shown in Polish', async () => {
    await setLanguage('pl');

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Zarejestrowani użytkownicy' })).toBeInTheDocument();
    expect(await screen.findAllByRole('button', { name: 'Resetuj hasło' })).toHaveLength(2);
    expect(screen.getByText(/Strona 1 z 1/)).toBeInTheDocument();
  });
});
