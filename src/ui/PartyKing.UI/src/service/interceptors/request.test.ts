import { AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { setLanguage } from '@/i18n';
import { onRequestFulfilled } from './request';

const request = (): InternalAxiosRequestConfig => ({ headers: new AxiosHeaders() });

describe('onRequestFulfilled', () => {
  it('sends the UI language as Accept-Language', () => {
    expect(onRequestFulfilled(request()).headers.get('Accept-Language')).toBe('en');
  });

  it('follows a language switch on the next request', async () => {
    await setLanguage('pl');

    expect(onRequestFulfilled(request()).headers.get('Accept-Language')).toBe('pl');
  });
});
