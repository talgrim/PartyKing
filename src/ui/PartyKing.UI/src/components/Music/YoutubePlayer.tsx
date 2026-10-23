import { ReactNode, useRef, useState } from "react";
import ReactPlayer from 'react-player';

type PlayerProps = {
    songUrl: string,
    onEnded?: () => void,
    mute: boolean,
    paused?: boolean,
    // Fired when playback is paused or resumed, including through the player's own controls.
    onPausedChange?: (paused: boolean) => void
}

export const YoutubePlayer = ({songUrl, mute, paused = false, onPausedChange, onEnded}: PlayerProps) => {
    const player = useRef(null);

    return (
        <ReactPlayer
            ref={player}
            src={songUrl}
            className="w-full h-full max-h-screen aspect-video"
            playing={!paused}
            volume={0.02}
            controls={true}
            muted={mute}
            onReady={() => {
                console.log('Player ready', player);
            }}
            onPlay={() => onPausedChange?.(false)}
            onPause={() => onPausedChange?.(true)}
            onEnded={onEnded}
            style={{ 
                width: '100%',
                height: 'auto'
            }}
        />
    )
}