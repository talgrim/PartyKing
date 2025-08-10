import { ChangeEvent, ReactNode, useState, FormEvent, useEffect } from "react";
import { makeUrlWithParams } from "@/service/fetcher/apiService";
import { axios } from "@/service/request";
import envVariables from "@/envVariables";

type Props = {
    playSongCallback: Function,
    isPlaying: string
}

type LocalSong = {
    url: string,
    type: string
}

const API_URL = makeUrlWithParams(`${envVariables.apiUrl}/queue-song`);

const getLocalPlaylist = () => {
    return JSON.parse(localStorage.getItem('localPlaylist')) || [];
}

const storeLocalPlaylist = (song: LocalSong) => {
    const playList = getLocalPlaylist();
    playList.push(song);
    localStorage.setItem('localPlaylist', JSON.stringify(playList));

    return playList;
}

const getSongType = (url: string): string => {
    return url.includes('youtu') ? 'youtube' : 'spotify';
}

export const Queue = ({playSongCallback, isPlaying}: Props): ReactNode => {
    const [url, setUrl] = useState('');
    const [playlist, setPlaylist] = useState([]);

    useEffect(() => {
        setPlaylist(getLocalPlaylist());
    }, [])

    const handleChange = (e: ChangeEvent) => {
        setUrl(e.target.value);
    }

    const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        
        try {
            const response = await axios.get(API_URL.toString(), {
                params: {
                    url: url
                }
            });

            const localSong: LocalSong = {
                url: url,
                type: getSongType(url)
            }

            setPlaylist(storeLocalPlaylist(localSong));

            console.log(response);
            setUrl('');
        } catch (error) {
            console.log(error);
        }
    }

    console.log(playlist);

    return (
        <div>
            <form className="flex mt-4 mb-4" onSubmit={handleSubmit}>
                <input type="text" value={url} onChange={handleChange}className="w-full px-2 py-2 border-1 border-gray-700 rounded-sm focus:outline-none" placeholder="Insert youtube URL" />
                <button type="submit" className="bg-slate-900 w-16 cursor-pointer">Add</button>
            </form>
            {playlist.map((song: LocalSong) => 
                <div className={`${isPlaying === song.url ? 'bg-neutral-600' : 'bg-neutral-800'} rounded-md cursor-pointer mb-4 p-4 hover:bg-neutral-900`}
                    key={song.url}
                    onClick={() => playSongCallback(song.url)}
                >
                    <p>{song.url}</p>
                    <p>{song.type}</p>
                </div>
            )}
        </div>
    )
}