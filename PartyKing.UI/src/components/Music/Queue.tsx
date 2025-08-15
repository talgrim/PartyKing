import { ChangeEvent, ReactNode, useState, FormEvent } from "react";
import { makeUrlWithParams } from "@/service/fetcher/apiService";
import { axios } from "@/service/request";
import envVariables from "@/envVariables";

type Props = {
    songSwitch: Function
}

const API_URL = makeUrlWithParams(`${envVariables.apiUrl}/queue-song`);

export const Queue = (): ReactNode => {
    const [url, setUrl] = useState('');
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

            console.log(response);
            setUrl('');
        } catch (error) {
            console.log(error);
        }
    }

    return (
        <>
            <form className="flex mt-4 mb-4 px-2" onSubmit={handleSubmit}>
                <input type="text" value={url} onChange={handleChange}className="w-full px-2 py-2 border-1 border-gray-700 rounded-sm focus:outline-none" placeholder="Insert youtube URL" />
                <button type="submit" className="bg-slate-900 w-16 cursor-pointer">Add</button>
            </form>
        </>
    )
}