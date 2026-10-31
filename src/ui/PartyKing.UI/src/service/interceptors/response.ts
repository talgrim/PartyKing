import { AxiosResponse } from 'axios';
import { i18n } from '@/i18n';
import { ApiErrorOptions } from '@/service/ApiError';

export const onResponseFulfilled = <T, D>(response: AxiosResponse<T, D>): T => {
  return response.data;
};

type ProblemDetails = { title?: string; detail?: string; errors?: Record<string, string[]> };

/** The parts of a rejected axios request that are read here. */
type RejectedRequest = {
  code?: string;
  message?: string;
  response?: { data?: ProblemDetails | string; status?: number; statusText?: string };
};

// ASP.NET validation problems carry the readable reason in `errors`, other problems in `title`.
const readableMessage = (response: RejectedRequest): string => {
  const data = response.response?.data;
  // Some endpoints (photo upload) answer a rejection with a plain text reason
  if (typeof data === 'string' && data) return data;
  const problem = typeof data === 'object' ? data : undefined;
  const validationErrors = Object.values(problem?.errors ?? {}).flat();

  return problem?.detail || validationErrors[0] || problem?.title || response.message || i18n.t('apiErrors.requestFailed');
};

export const onResponseRejected = (response: RejectedRequest) => {
  if (response.code === 'ERR_CANCELED') {
    return;
  }

  const data = response.response?.data;
  const responseError: ApiErrorOptions = {
    title: typeof data === 'object' ? data?.title : undefined,
    message: readableMessage(response),
    status: response.response?.status,
    statusText: response.response?.statusText,
  };

  return Promise.reject(responseError);
};
