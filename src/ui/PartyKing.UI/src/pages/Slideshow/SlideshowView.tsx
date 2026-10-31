import useSWR from 'swr';
import { useTranslation } from 'react-i18next';
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
import {slideshowKey} from "@/service/realtime/sessionMusicEvents";
import {useSlideshowWatch} from "@/service/realtime/useSlideshowWatch";

export type ApiImage = {
  fileName: string,
  expirationDate: Date
}

// Slide changes are pushed while the hub is up; the poll only covers a missed push or a hub that is down
const LIVE_POLL_MS = 60_000;
const FALLBACK_POLL_MS = 5_000;

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
  const { t } = useTranslation();
  const live = useSlideshowWatch();
  const {data, isLoading, error, mutate} = useSWR<ApiImage, ApiError>(
    slideshowKey(sessionId),
    fetchFromApi,
    {
      revalidateOnFocus: false,
      refreshInterval: live ? LIVE_POLL_MS : FALLBACK_POLL_MS,
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
        {t('slideshowView.loadFailed')}{' '}
        <Link to={ROUTE_PATHS.HOME} className="underline">{t('common.backToSessions')}</Link>
      </p>
    );
  }

  // 204 No Content (nothing uploaded yet) arrives as an empty string
  if (!data) return (
    <div className="flex h-full w-full flex-col items-center justify-center gap-2 p-4 text-center">
      <h1 className="text-2xl font-semibold">{t('slideshowView.noPhotos')}</h1>
      <p>{t('slideshowView.uploadSome')}</p>
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
