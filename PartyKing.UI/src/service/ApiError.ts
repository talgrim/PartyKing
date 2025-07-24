import { HTTP_STATUS_CODE } from './constants';

export interface ApiErrorOptions {
  title?: string;
  message: string;
  status?: number;
  statusText?: string;
}

const createError = (
  response: Response,
  title: string,
  message: string
): ApiError =>
  new ApiError({
    title,
    message,
    status: response.status,
    statusText: response.statusText,
  });

export class ApiError extends Error {
  static parseFromResponse = async (response: Response): Promise<ApiError> => {
    try {
      const respBody = await response.text();
      const respData = JSON.parse(respBody);

      if (Array.isArray(respData.detail)) {
        respData.detail = respData.detail
          .map((a: any) => `(${Object.values(a)})`)
          .join(', ');
      }

      return new ApiError({
        title: respData.title,
        message: respData.detail,
        status: response.status,
        statusText: response.statusText,
      });
    } catch (err) {
      switch (response.status) {
        case HTTP_STATUS_CODE.BAD_REQUEST:
          return createError(
            response,
            'Bad Request',
            'The request was invalid'
          );
        case HTTP_STATUS_CODE.UNAUTHORIZED:
          return createError(
            response,
            'Unauthorized',
            'You are not authorized to access this resource'
          );
        case HTTP_STATUS_CODE.FORBIDDEN:
          return createError(
            response,
            'Forbidden',
            'You do not have permission to access this resource'
          );
        case HTTP_STATUS_CODE.NOT_FOUND:
          return createError(
            response,
            'Not Found',
            'The requested resource was not found'
          );
        case HTTP_STATUS_CODE.INTERNAL_SERVER_ERROR:
          return createError(
            response,
            'Internal Server Error',
            'Something went wrong'
          );
        case HTTP_STATUS_CODE.BAD_GATEWAY:
          return createError(response, 'Bad Gateway', 'Something went wrong');
        case HTTP_STATUS_CODE.SERVICE_UNAVAILABLE:
          return createError(
            response,
            'Service Unavailable',
            'Something went wrong'
          );
        case HTTP_STATUS_CODE.GATEWAY_TIMEOUT:
          return createError(
            response,
            'Gateway Timeout',
            'Something went wrong'
          );
      }

      return createError(response, 'Unknown Error', 'Please check the logs');
    }
  };

  public message: string;
  public title?: string;
  public status?: number;
  public statusText?: string;

  constructor({ message, title, status, statusText }: ApiErrorOptions) {
    super(message);
    this.message = message;
    this.title = title;
    this.status = status;
    this.statusText = statusText;
    Object.setPrototypeOf(this, ApiError.prototype);
  }
}
