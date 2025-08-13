import useSWR from 'swr';
import { Splide, SplideSlide, Options } from '@splidejs/react-splide';
import {fetchFromApi} from "@/service/fetcher/apiService";
import {ApiError} from "@/service/ApiError";
import '@splidejs/react-splide/css';
import { Backdrop, CircularProgress } from '@mui/material';
import { ReactNode } from 'react';
import envVariables from "@/envVariables";
import {Slide} from "@/pages/Slideshow/Slide";

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
  cover: false,
  arrows: false,
  pagination: false
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
  }

  if(!data) return (
    <Backdrop open>
      <CircularProgress />
    </Backdrop>
  )

  return (
    <div className="grid grid-cols-12">
      <div className="photos-slideshow col-span-8">
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
