import { axios } from '@/service/request';
import { AccountDto } from './types';

const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

const withCredentials = { withCredentials: true };

export const accountApi = {
  /** Makes a user account and signs it in. Rejects with status 409 when the username is taken. */
  register: (username: string, password: string) =>
    unwrap<void>(axios.post('/account/register', { username, password }, withCredentials)),

  /** Signs a user or an admin in. Rejects with status 401 for wrong credentials. */
  login: (username: string, password: string) =>
    unwrap<void>(axios.post('/account/login', { username, password }, withCredentials)),

  logout: () => unwrap<void>(axios.post('/account/logout', null, withCredentials)),

  /** Ends every login of the account, this browser's too. */
  logoutEverywhere: () => unwrap<void>(axios.post('/account/logout-everywhere', null, withCredentials)),

  /** Who is signed in; rejects with status 401 when nobody is. */
  me: () => unwrap<AccountDto>(axios.get('/account/me', withCredentials)),

  /** Rejects with status 400 when the current password is wrong or the new one is too short. */
  changePassword: (currentPassword: string, newPassword: string) =>
    unwrap<void>(axios.put('/account/password', { currentPassword, newPassword }, withCredentials)),

  /** Deletes the signed-in user account after the password is confirmed (not available for admins). */
  deleteAccount: (password: string) =>
    unwrap<void>(axios.delete('/account', { ...withCredentials, data: { password } })),
};
