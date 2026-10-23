import { createRoot } from 'react-dom/client';
import './index.css';
import { RouterProvider } from 'react-router-dom';
import { router } from './router';
const root = createRoot(document.getElementById('root') as HTMLElement);

root.render(<RouterProvider router={router} />);
