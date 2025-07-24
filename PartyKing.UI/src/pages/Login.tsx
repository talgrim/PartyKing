import useSWR from 'swr';
import {fetchFromApi, SERVICE_ENDPOINT} from "@/service/fetcher/apiService";
import {ApiError} from "@/service/ApiError";
import {HTTP_STATUS_CODE} from "@/service/constants";
import {ErrorMessage} from "@/components/common/ErrorMessage";

export const Login = () => {

  const {data, error} = useSWR<string, ApiError>(
    {
      endpoint: SERVICE_ENDPOINT.Spotify().authUrl.GET,
    },
    fetchFromApi,
    {
      onErrorRetry: (e, _, config, revalidate, {retryCount}) => {
        if (e.status === HTTP_STATUS_CODE.NOT_FOUND) return;
        if (e.status === HTTP_STATUS_CODE.UNAUTHORIZED) return;
        setTimeout(() => revalidate({retryCount}), config.errorRetryInterval);
      },
      revalidateOnFocus: false,
    },
  );

  if (error) {
    return <ErrorMessage message={JSON.stringify(error)}/>;
  }

  return (
    <div className="App">
      <header className="App-header">
        <a
          className="App-link"
          href={data}
          rel="noopener noreferrer"
        >
          Login to spotify
        </a>
      </header>
    </div>
  );
};
