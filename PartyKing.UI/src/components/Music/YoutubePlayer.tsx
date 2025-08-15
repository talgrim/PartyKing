import { ReactNode, useRef, useState } from "react";
import ReactPlayer from 'react-player';

type PlayerProps = {
    songUrl: string,
    onEnded?: Function,
    mute: boolean
}

export const YoutubePlayer = ({songUrl, mute, onEnded}: PlayerProps) => {
    const player = useRef(null);

    return (
        <ReactPlayer
            ref={player}
            src={songUrl}
            className="w-full h-full max-h-screen aspect-video"
            playing={true}
            volume={0.02}
            controls={true}
            muted={mute}
            onReady={() => {
                console.log('Player ready', player);
            }}
            onPlay={() => console.log('play')}
            onEnded={onEnded}
            style={{ 
                width: '100%',
                height: 'auto'
            }}
        />
    )
}