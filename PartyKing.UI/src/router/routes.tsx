import {RouteObject} from 'react-router-dom';
import {ROUTE_PATHS} from './constants';
import {App} from "@/App";
import {Callback} from "@/pages/Callback";
import {Login} from "@/pages/Login";
import {Home} from "@/pages/Home";

export const routes: RouteObject[] = [
  {
    path: ROUTE_PATHS.HOME,
    element: <App/>,
    children: [
      {path: '', element: <Home/>},
      {path: ROUTE_PATHS.CALLBACK, element: <Callback/>},
      {path: ROUTE_PATHS.LOGIN, element: <Login/>},
    ],
  },
];
