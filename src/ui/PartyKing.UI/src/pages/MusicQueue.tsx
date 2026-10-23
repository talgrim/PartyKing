import { ReactNode } from 'react';
import { Queue } from '@/components/Music/Queue';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ROUTE_PATHS } from '@/router/constants';

/** Guest page: anyone can search for or paste songs and add them to the queue. No login needed. */
export const MusicQueue = (): ReactNode => {
  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="max-w-xl mx-auto">
        <PageNavigation
          className="px-2 pt-4"
          links={[
            { to: ROUTE_PATHS.SLIDESHOW, label: 'Slideshow' },
            { to: ROUTE_PATHS.IMAGE_UPLOADER, label: 'Upload photos' },
          ]}
        />
        <h1 className="px-2 pt-4 text-2xl text-slate-200">Add a song to the queue</h1>
        <Queue />
      </div>
    </div>
  );
};
