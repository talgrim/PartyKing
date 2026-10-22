import { AxiosResponse } from 'axios';
import { ApiErrorOptions } from '@/service/ApiError';

export const onResponseFulfilled = <T, D>(response: AxiosResponse<T, D>): T => {
  return response.data;
};
export const onResponseRejected = (response?: any) => {
  if (response.code === 'ERR_CANCELED') {
    return;
  }

  const responseError: ApiErrorOptions = {
    title: response?.response?.data?.title,
    message: response?.response?.data?.detail || response?.message,
    status: response?.response?.status,
    statusText: response?.response?.statusText,
  };

  return Promise.reject(responseError);
};
