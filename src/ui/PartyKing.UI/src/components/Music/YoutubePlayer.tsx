import { ReactNode, useRef, useState } from "react";
import ReactPlayer from 'react-player';

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
    onError?: (code: number | undefined) => void
}

export const YoutubePlayer = ({songUrl, mute, volume = 0.5, paused = false, onPausedChange, onEnded, onError}: PlayerProps) => {
    const player = useRef(null);

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
            onError={(event) => onError?.((event.target as { error?: { code?: number } } | null)?.error?.code)}
            style={{ 
                width: '100%',
                height: 'auto'
            }}
        />
    )
}