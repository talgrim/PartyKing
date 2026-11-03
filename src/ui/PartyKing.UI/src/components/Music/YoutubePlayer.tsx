import { Ref, useImperativeHandle, useRef } from "react";
import ReactPlayer from 'react-player';

export type YoutubePlayerHandle = {
    /** Where in the video the player is now. */
    getPositionMs: () => number,
}

type PlayerProps = {
    songUrl: string,
    onEnded?: () => void,
    mute: boolean,
    /** 0 to 1. */
    volume?: number,
    paused?: boolean,
    // Fired when playback is paused or resumed, including through the player's own controls.
    onPausedChange?: (paused: boolean) => void,
    // Fired when YouTube refuses to play the video, e.g. the owner disabled embedding (code 101 or 150).
    onError?: (code: number | undefined) => void,
    // Fired when the viewer jumps to another position in the video.
    onSeeked?: () => void,
    handle?: Ref<YoutubePlayerHandle>
}

export const YoutubePlayer = ({songUrl, mute, volume = 0.5, paused = false, onPausedChange, onEnded, onError, onSeeked, handle}: PlayerProps) => {
    const player = useRef<HTMLVideoElement>(null);

    useImperativeHandle(handle, () => ({
        getPositionMs: () => Math.round((player.current?.currentTime ?? 0) * 1000),
    }));

    return (
        <ReactPlayer
            ref={player}
            src={songUrl}
            className="w-full h-full max-h-screen aspect-video"
            playing={!paused}
            volume={volume}
            controls={true}
            muted={mute}
            onReady={() => {
                console.log('Player ready', player);
            }}
            onPlay={() => onPausedChange?.(false)}
            onPause={() => onPausedChange?.(true)}
            onEnded={onEnded}
            onSeeked={() => onSeeked?.()}
            onError={(event) => onError?.((event.target as { error?: { code?: number } } | null)?.error?.code)}
            style={{ 
                width: '100%',
                height: 'auto'
            }}
        />
    )
}