import useSWR from 'swr';
import { adminApi } from './adminApi';

/** True once the admin cookie is confirmed valid. Shares its cache entry with the admin panel's own check. */
export const useIsAdmin = (): boolean => {
  const { data } = useSWR('admin-me', () => adminApi.me().then(() => true), {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
  });

  return data === true;
};
