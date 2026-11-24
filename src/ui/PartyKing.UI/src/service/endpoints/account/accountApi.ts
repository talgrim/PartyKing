import { identityAxios } from '@/service/request';
import { AccountDto } from './types';

const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

/**
 * What the UI asks of the identity service. Everything else about an account (logging in, registering, the password,
 * deleting it) happens on its own pages, which the header links to.
 */
export const accountApi = {
  /** Who is signed in; rejects with status 401 when nobody is. */
  me: () => unwrap<AccountDto>(identityAxios.get('/api/me')),

  /** Ends this browser's login. */
  logout: () => unwrap<void>(identityAxios.post('/api/logout')),
};
