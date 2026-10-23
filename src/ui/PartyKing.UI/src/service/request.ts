import axiosObject, { AxiosInstance } from 'axios';
import envVariables from '@/envVariables';
import {
  onResponseFulfilled,
  onResponseRejected,
} from '@/service/interceptors/response';

const configureAxios = (
  axios: AxiosInstance,
  baseUrl = envVariables.apiUrl,
) => {
  axios.defaults.baseURL = baseUrl;
  axios.defaults.headers.common['Accept'] = 'application/json';
  axios.defaults.headers.common['Content-Type'] = 'application/json';
  axios.defaults.validateStatus = (status) => {
    return status < 400;
  };
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
