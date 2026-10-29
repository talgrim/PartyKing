import axiosObject, { AxiosInstance } from 'axios';
import envVariables from '@/envVariables';
import {
  onResponseFulfilled,
  onResponseRejected,
} from '@/service/interceptors/response';
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
  axios.interceptors.response.use(onResponseFulfilled, onResponseRejected);
};

export const axios = axiosObject.create();
configureAxios(axios);

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
