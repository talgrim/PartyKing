import useSWR from 'swr';
import { Splide, Options } from '@splidejs/react-splide';
import {fetchFromApi} from "@/service/fetcher/apiService";
import {ApiError} from "@/service/ApiError";
import '@splidejs/react-splide/css';
import { CircularProgress } from '@mui/material';
import { Link } from 'react-router-dom';
import {Slide} from "@/pages/Slideshow/Slide";
import {SessionPasswordForm} from "@/components/Music/SessionPasswordForm";
import {HTTP_STATUS_CODE} from "@/service/constants";
import {ROUTE_PATHS} from "@/router/constants";

export type ApiImage = {
  fileName: string,
  expirationDate: Date
}

const SliderConfig: Options  = {
  type: 'fade',
  rewind : true,
  perPage: 1,
  autoplay: true,
  drag: false,
  height: '100%',
  width: '100%',
  cover: false,
  arrows: false,
  pagination: false,
  waitForTransition: true
}

type Props = {
  sessionId: string;
};

/**
 * The photos of one session, one at a time; behind the session password like the rest of the session. It fills the
 * box its parent gives it, so the parent decides how big the slideshow is.
 */
export const SlideshowView = ({ sessionId }: Props) => {
  const {data, isLoading, error, mutate} = useSWR<ApiImage, ApiError>(
    {
      endpoint: `/sessions/${sessionId}/photos/current`,
    },
    fetchFromApi,
    {
      revalidateOnFocus: false,
      refreshInterval: 5000,
      shouldRetryOnError: false,
    },
  );

  if (isLoading) return (
    <div className="flex h-full w-full items-center justify-center">
      <CircularProgress />
    </div>
  )

  if (error?.status === HTTP_STATUS_CODE.UNAUTHORIZED) return (
    <div className="mx-auto max-w-xl text-slate-400">
      <SessionPasswordForm sessionId={sessionId} onJoined={() => void mutate()} />
    </div>
  )

  if (error) {
    return (
      <p className="p-4 text-center">
        Could not load the slideshow.{' '}
        <Link to={ROUTE_PATHS.HOME} className="underline">Back to the sessions</Link>
      </p>
    );
  }

  // 204 No Content (nothing uploaded yet) arrives as an empty string
  if (!data) return (
    <div className="flex h-full w-full flex-col items-center justify-center gap-2 p-4 text-center">
      <h1 className="text-2xl font-semibold">No photos yet</h1>
      <p>Upload some photos and they will show up here.</p>
    </div>
  )

  return (
    <div className="photos-slideshow">
      <Splide
        options={ SliderConfig }
        aria-labelledby="dynamic-slides-example-heading"
      >
        <Slide {...data}/>
      </Splide>
    </div>
  );
};
