import { ReactNode } from 'react';
import { NowPlayingDto } from '@/service/endpoints/player/types';
import { useTranslation } from 'react-i18next';

type Props = {
  nowPlaying: NowPlayingDto | undefined;
};

/**
 * The song playing right now as plain text: the title and the artist (a YouTube video has the video's title and no
 * artist). No art, so it fits next to other content, e.g. a game.
 */
export const NowPlayingLine = ({ nowPlaying }: Props): ReactNode => {
  const { t } = useTranslation();
  if (!nowPlaying) return null;

  const { song, paused, playerConnected } = nowPlaying;

  return (
    <section aria-label={t('nowPlaying.title')} className="p-3 bg-slate-900 rounded-lg">
      <h2 className="text-xs font-semibold tracking-wider text-amber-500 uppercase">{t('nowPlaying.title')}</h2>

      {!song ? (
        <p className="pt-1 text-sm text-slate-500">{t('nowPlaying.nothing')}</p>
      ) : (
        <div className="pt-1 min-w-0">
          <div className="font-semibold text-slate-100 truncate">{song.name}</div>
          {song.artists && <div className="text-sm truncate">{song.artists}</div>}
          {paused && (
            <span className="inline-block mt-1 px-2 py-0.5 text-xs text-amber-500 bg-slate-800 rounded-full">
              {t('nowPlaying.paused')}
            </span>
          )}
          {!playerConnected && <p className="mt-1 text-sm text-slate-500">{t('nowPlaying.playerNotConnected')}</p>}
        </div>
      )}
    </section>
  );
};
