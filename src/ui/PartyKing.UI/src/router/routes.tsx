import {RouteObject} from 'react-router-dom';
import {ROUTE_PATHS} from './constants';
import {App} from "@/App";
import {Callback} from "@/pages/Callback";
import {Home} from "@/pages/Home";
import { Slideshow } from '@/pages/Slideshow/Slideshow';
import { ImageUploader } from '@/pages/ImageUploader';
import { MusicQueue } from '@/pages/MusicQueue';
import { Admin } from '@/pages/Admin/Admin';

export const routes: RouteObject[] = [
  {
    path: ROUTE_PATHS.HOME,
    element: <App/>,
    children: [
      {path: '', element: <Home/>},
      {path: ROUTE_PATHS.CALLBACK, element: <Callback/>},
      {path: ROUTE_PATHS.SLIDESHOW, element: <Slideshow/>},
      {path: ROUTE_PATHS.IMAGE_UPLOADER, element: <ImageUploader/>},
      {path: ROUTE_PATHS.ADMIN, element: <Admin/>},
      {path: ROUTE_PATHS.SESSION, element: <MusicQueue/>},
    ],
  },
];
