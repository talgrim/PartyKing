import {RouteObject} from 'react-router-dom';
import {ROUTE_PATHS} from './constants';
import {App} from "@/App";
import {Callback} from "@/pages/Callback";
import {Login} from "@/pages/Login";
import {Home} from "@/pages/Home";
import { Slideshow } from '@/pages/Slideshow';
import { ImageUploader } from '@/pages/ImageUploader';
import { Music } from '@/pages/Music';

export const routes: RouteObject[] = [
  {
    path: ROUTE_PATHS.HOME,
    element: <App/>,
    children: [
      {path: '', element: <Home/>},
      {path: ROUTE_PATHS.CALLBACK, element: <Callback/>},
      {path: ROUTE_PATHS.LOGIN, element: <Login/>},
      {path: ROUTE_PATHS.SLIDESHOW, element: <Slideshow/>},
      {path: ROUTE_PATHS.IMAGE_UPLOADER, element: <ImageUploader/>},
      {path: ROUTE_PATHS.MUSIC, element: <Music/>},
    ],
  },
];
