import axiosObject, { AxiosInstance } from 'axios';
import envVariables from '@/envVariables';
import {
  onResponseFulfilled,
  onResponseRejected,
} from '@/service/interceptors/response';
import { onRequestFulfilled } from '@/service/interceptors/request';
import { devPersona } from '@/service/devPersona';

const configureAxios = (
  axios: AxiosInstance,
  baseUrl = envVariables.apiUrl,
) => {
  axios.defaults.baseURL = baseUrl;
  // Session calls need the cookie that remembers the entered session password (cross-origin in dev)
  axios.defaults.withCredentials = true;
  axios.defaults.headers.common['Accept'] = 'application/json';
  axios.defaults.headers.common['Content-Type'] = 'application/json';
  axios.defaults.validateStatus = (status) => {
    return status < 400;
  };
  if (devPersona) {
    axios.defaults.headers.common['X-Dev-Persona'] = devPersona;
  }
  axios.interceptors.request.use(onRequestFulfilled);
  axios.interceptors.response.use(onResponseFulfilled, onResponseRejected);
};

export const axios = axiosObject.create();
configureAxios(axios);

/**
 * The identity service (auth.talgrim.dev), called cross-origin with the session cookie. It is not the API: no dev
 * persona and no Accept-Language go to it, and its 401 means nobody is signed in, like the API's.
 */
export const identityAxios = axiosObject.create({
  baseURL: envVariables.identityUrl,
  withCredentials: true,
  headers: { Accept: 'application/json' },
  validateStatus: (status) => status < 400,
});
identityAxios.interceptors.response.use(onResponseFulfilled, onResponseRejected);

export const addAccessTokenToApiHeaders = (accessToken?: string) => {
  addAuthorizationHeader(axios, accessToken);
};

const addAuthorizationHeader = (
  axios: AxiosInstance,
  accessToken?: string,
) => {
  axios.defaults.headers.common[
    'Authorization'
  ] = `Bearer ${accessToken}`;
};
