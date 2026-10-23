import useSWR from 'swr';
import { Splide, SplideSlide, Options } from '@splidejs/react-splide';
import {fetchFromApi} from "@/service/fetcher/apiService";
import {ApiError} from "@/service/ApiError";
import '@splidejs/react-splide/css';
import { Backdrop, CircularProgress } from '@mui/material';
import { ReactNode } from 'react';
import envVariables from "@/envVariables";
import {Slide} from "@/pages/Slideshow/Slide";
import {PageNavigation} from "@/components/common/PageNavigation";
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

export const Slideshow = () => {
  const {data, isLoading, error} = useSWR<ApiImage, ApiError>(
    {
      endpoint: '/get-photo',
    },
    fetchFromApi,
    {
      revalidateOnFocus: false,
      refreshInterval: 5000,
    },
  );

  console.log(data);
  if (isLoading) return (
    <Backdrop open>
    <CircularProgress />
    </Backdrop>
  )

  if (error) {
    console.log(error);
    return <p className="p-4 text-center">Could not load the slideshow.</p>;
  }

  // 204 No Content (nothing uploaded and no placeholders) arrives as an empty string
  if (!data) return (
    <div className="flex h-screen flex-col items-center justify-center gap-2 text-center">
      <PageNavigation
        className="fixed top-2 right-2 z-10 opacity-30 hover:opacity-100"
        links={[
          {to: ROUTE_PATHS.IMAGE_UPLOADER, label: 'Upload photos'},
          {to: ROUTE_PATHS.HOME, label: 'Sessions'},
        ]}
      />
      <h1 className="text-2xl font-semibold">No photos yet</h1>
      <p>Upload some photos and they will show up here.</p>
    </div>
  )

  return (
    <div className="grid grid-cols-12">
      <PageNavigation
        className="fixed top-2 right-2 z-10 opacity-30 hover:opacity-100"
        links={[
          {to: ROUTE_PATHS.IMAGE_UPLOADER, label: 'Upload photos'},
          {to: ROUTE_PATHS.HOME, label: 'Sessions'},
        ]}
      />
      <div className="photos-slideshow">
        <Splide
          options={ SliderConfig }
          aria-labelledby="dynamic-slides-example-heading"
        >
          <Slide {...data}/>
        </Splide>
      </div>
    </div>
  );
};
