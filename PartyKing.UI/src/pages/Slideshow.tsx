import useSWR from 'swr';
import { Splide, SplideSlide, Options } from '@splidejs/react-splide';
import {fetchFromApi} from "@/service/fetcher/apiService";
import {ApiError} from "@/service/ApiError";
import '@splidejs/react-splide/css';
import { Backdrop, CircularProgress } from '@mui/material';
import { ReactNode } from 'react';

type ApiImage = {
  fileName: string,
  path: string
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
  const {data, isLoading, error} = useSWR<string, ApiError>(
    {
      endpoint: '/get-all',
    },
    fetchFromApi,
    {
      revalidateOnFocus: false,
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
  
  return (
    <div className="grid grid-cols-12">
      <div className="photos-slideshow col-span-8">
        <Splide
          options={ SliderConfig }
          aria-labelledby="dynamic-slides-example-heading"
        >
          {data && data.map((image: ApiImage): ReactNode => {
              return (
                <SplideSlide key={image.fileName}>
                  <img src={image.path} />
                </SplideSlide>
              )
            })
          }
        </Splide>
      </div>
    </div>
  );
};
