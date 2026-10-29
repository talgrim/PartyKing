import {RouteObject} from 'react-router-dom';
import {ROUTE_PATHS} from './constants';
import {App} from "@/App";
import {Callback} from "@/pages/Callback";
import {Home} from "@/pages/Home";
import { UploadRedirect } from '@/pages/UploadRedirect';
import { MusicQueue } from '@/pages/MusicQueue';
import { Admin } from '@/pages/Admin/Admin';
import { Games } from '@/pages/Games/Games';
import { Resistance } from '@/pages/Games/Resistance';
import { Ringo } from '@/pages/Games/Ringo';
import { RingoJoin } from '@/pages/Games/RingoJoin';

export const routes: RouteObject[] = [
  {
    path: ROUTE_PATHS.HOME,
    element: <App/>,
    children: [
      {path: '', element: <Home/>},
      {path: ROUTE_PATHS.CALLBACK, element: <Callback/>},
      {path: ROUTE_PATHS.SESSION_UPLOAD, element: <UploadRedirect/>},
      {path: ROUTE_PATHS.ADMIN, element: <Admin/>},
      {path: ROUTE_PATHS.SESSION_GAMES, element: <Games/>},
      {path: ROUTE_PATHS.SESSION_GAMES_RESISTANCE, element: <Resistance/>},
      {path: ROUTE_PATHS.SESSION_GAMES_RINGO, element: <Ringo/>},
      {path: ROUTE_PATHS.SESSION_GAMES_RINGO_JOIN, element: <RingoJoin/>},
      {path: ROUTE_PATHS.SESSION, element: <MusicQueue/>},
    ],
  },
];
