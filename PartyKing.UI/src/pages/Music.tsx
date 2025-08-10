import { ReactNode, useEffect, useState } from "react";
import { fetchFromApi } from "@/service/fetcher/apiService";
import { ApiError } from "@/service/ApiError";
import { CircularProgress } from "@mui/material";
import useSWR from "swr";
import { Queue } from "@/components/Music/Queue";
import { YoutubePlayer } from "@/components/Music/YoutubePlayer";

export const Music = (): ReactNode => {
    const [song, setSong] = useState(null);
    const [mute, setMute] = useState(true);
    const {data, isLoading, error, mutate} = useSWR<string, ApiError>(
        {
            endpoint: '/get-next',
        },
        fetchFromApi,
        {
            revalidateOnFocus: false,
        },
    );
    
    if (error) {
        console.log(error);
    }

    const getNextSong = async () => {
        const response = await mutate();
        console.log('Loading next song', response);
        playSong(response?.url)
    }

    const playSong = (url: string) => {
        setSong(url);
        setMute(false)
    }

    useEffect(() => {
        if (data) {
            setSong(data?.url);
            console.log('Song changed');
        }
    }, [data]);

    return (
        <div className="w-full h-full grid grid-cols-12 bg-slate-800 text-slate-400">
            <div className="col-span-7 place-content-center">
                {isLoading && 
                    <CircularProgress />
                }
                
                {song &&
                    <YoutubePlayer songUrl={song} mute={mute} show={true}/>
                }
            </div>
            <div className="col-span-5 px-2">
                <Queue isPlaying={song} playSongCallback={playSong}/>
                <div className="space-x-4">
                    <button
                        onClick={() => {
                            setMute(false);
                        }}
                        className="p-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer"
                    >
                        Unmute
                    </button>
                    
                    <button onClick={getNextSong} className="p-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer">
                        Next song
                    </button>
                </div>
            </div>
        </div>
    );
}