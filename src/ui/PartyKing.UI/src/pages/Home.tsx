import useSWR from 'swr';
import {fetchFromApi, SERVICE_ENDPOINT} from "@/service/fetcher/apiService";
import {ApiError} from "@/service/ApiError";
import {HTTP_STATUS_CODE} from "@/service/constants";
import {ErrorMessage} from "@/components/common/ErrorMessage";
import {useNavigate} from "react-router-dom";
import {InfoMessage} from "@/components/common/InfoMessage";
import {ROUTE_PATHS} from "@/router/constants";
import {useEffect, useState} from "react";
import {Backdrop, CircularProgress} from "@mui/material";

export const Home = () => {
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const navigate = useNavigate();

  const {data, error} = useSWR<string, ApiError>(
    {
      endpoint: SERVICE_ENDPOINT.Spotify().currentlyPlaying.GET,
    },
    fetchFromApi,
    {
      onErrorRetry: (e, _, config, revalidate, {retryCount}) => {
        if (e.status === HTTP_STATUS_CODE.NOT_FOUND) return;
        if (e.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
          navigate(ROUTE_PATHS.LOGIN, {replace: true});
          return;
        }
        setTimeout(() => revalidate({retryCount}), config.errorRetryInterval);
      },
      revalidateOnFocus: false,
    },
  );

  useEffect(() => {
    setIsLoading(data === undefined);
  }, [data])

  if (isLoading) {
    return <Backdrop open>
      <CircularProgress/>
    </Backdrop>
  }

  if (error) {
    return <ErrorMessage message={JSON.stringify(error)}/>;
  }

  const message = `Authorized. Current song: ${data}`;

  return <InfoMessage message={message}/>;
};