import { axios } from '@/service/request';
import { adminApi } from './adminApi';

vi.mock('@/service/request', () => ({ axios: { delete: vi.fn() } }));

describe('adminApi', () => {
  it('cancels the deletion a guest asked for, with the cookies', async () => {
    vi.mocked(axios.delete).mockResolvedValue(undefined);

    await adminApi.cancelSessionDeletion('session-1');

    expect(axios.delete).toHaveBeenCalledWith('/admin/sessions/session-1/deletion-request', {
      withCredentials: true,
    });
  });
});
