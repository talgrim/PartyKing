import {SplideSlide} from "@splidejs/react-splide";
import envVariables from "@/envVariables";
import {ApiImage} from "@/pages/Slideshow/Slideshow";
import {Options} from "@splidejs/splide";

const API_URL = envVariables.apiUrl;

export const Slide = (image: ApiImage) => {

  return (
    <SplideSlide key={image.fileName}>
    <img src={`${API_URL}/${image.fileName}`} />
  </SplideSlide>
)
}