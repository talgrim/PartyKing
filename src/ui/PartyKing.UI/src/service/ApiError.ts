import { i18n } from '@/i18n';
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
          .map((a: object) => `(${Object.values(a)})`)
          .join(', ');
      }

      return new ApiError({
        title: respData.title,
        message: respData.detail,
        status: response.status,
        statusText: response.statusText,
      });
    } catch {
      switch (response.status) {
        case HTTP_STATUS_CODE.BAD_REQUEST:
          return createError(
            response,
            i18n.t('apiErrors.badRequest'),
            i18n.t('apiErrors.badRequestMessage')
          );
        case HTTP_STATUS_CODE.UNAUTHORIZED:
          return createError(
            response,
            i18n.t('apiErrors.unauthorized'),
            i18n.t('apiErrors.unauthorizedMessage')
          );
        case HTTP_STATUS_CODE.FORBIDDEN:
          return createError(
            response,
            i18n.t('apiErrors.forbidden'),
            i18n.t('apiErrors.forbiddenMessage')
          );
        case HTTP_STATUS_CODE.NOT_FOUND:
          return createError(
            response,
            i18n.t('apiErrors.notFound'),
            i18n.t('apiErrors.notFoundMessage')
          );
        case HTTP_STATUS_CODE.INTERNAL_SERVER_ERROR:
          return createError(
            response,
            i18n.t('apiErrors.internal'),
            i18n.t('common.somethingWentWrong')
          );
        case HTTP_STATUS_CODE.BAD_GATEWAY:
          return createError(
            response,
            i18n.t('apiErrors.badGateway'),
            i18n.t('common.somethingWentWrong')
          );
        case HTTP_STATUS_CODE.SERVICE_UNAVAILABLE:
          return createError(
            response,
            i18n.t('apiErrors.unavailable'),
            i18n.t('common.somethingWentWrong')
          );
        case HTTP_STATUS_CODE.GATEWAY_TIMEOUT:
          return createError(
            response,
            i18n.t('apiErrors.gatewayTimeout'),
            i18n.t('common.somethingWentWrong')
          );
      }

      return createError(
        response,
        i18n.t('apiErrors.unknown'),
        i18n.t('apiErrors.unknownMessage')
      );
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
