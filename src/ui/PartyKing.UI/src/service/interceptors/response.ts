import { AxiosResponse } from 'axios';
import { ApiErrorOptions } from '@/service/ApiError';

export const onResponseFulfilled = <T, D>(response: AxiosResponse<T, D>): T => {
  return response.data;
};

// ASP.NET validation problems carry the readable reason in `errors`, other problems in `title`.
const readableMessage = (response?: any): string => {
  const data = response?.response?.data;
  const validationErrors: string[] = Object.values(data?.errors ?? {}).flat() as string[];

  return data?.detail || validationErrors[0] || data?.title || response?.message || 'Request failed';
};

export const onResponseRejected = (response?: any) => {
  if (response.code === 'ERR_CANCELED') {
    return;
  }

  const responseError: ApiErrorOptions = {
    title: response?.response?.data?.title,
    message: readableMessage(response),
    status: response?.response?.status,
    statusText: response?.response?.statusText,
  };

  return Promise.reject(responseError);
};
