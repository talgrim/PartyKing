import {SERVICE_ENDPOINT} from '@/service/fetcher/apiService';
import {axios} from '../../request';
import {Authorize} from "@/service/endpoints/users/users.types";

const authorize: Authorize = ({body}) =>
  axios.post(
    SERVICE_ENDPOINT.Spotify().authenticate.POST,
    body,
    {withCredentials: true}
  );

export const usersApi = {
  authorize,
};
