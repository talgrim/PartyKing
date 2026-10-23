import { API_METHOD } from "../constants";
import { ApiError } from "../ApiError";
import envVariables from "@/envVariables";

export const SERVICE_ENDPOINT = {
    Spotify: () => {
        return {
            authenticate: {
                POST: `/authenticate`
            }
        };
    },
} as const;

export const makeUrlWithParams = (
    url: string,
    params?: Record<string, string>,
) => {
    // The base makes relative API URLs (e.g. /api/...) work
    const base = window.location.origin;
    if (!params) return new URL(url, base);
    const searchParams = new URLSearchParams(params).toString();
    if (!searchParams) return new URL(url, base);
    return new URL(`${url}?${searchParams}`, base);
};

export interface FetchFromApiOptions {
    endpoint: string;
    queryParams?: Record<string, string>;
}

export const fetchFromApi = async ({
                                       endpoint,
                                       queryParams,
                                   }: FetchFromApiOptions) => {
    const url = makeUrlWithParams(envVariables.apiUrl + endpoint, queryParams);
    const res = await fetch(url, {
        headers: {
            Accept: 'application/json',
            'Content-Type': 'application/json',
        },
        credentials: "include",
        method: API_METHOD.GET,
    });

    if (!res.ok && res.status !== 210) {
        throw await ApiError.parseFromResponse(res);
    }
    const contentType = res.headers.get('content-type');
    if (!contentType || !contentType.includes('application/json'))
        return res.text();
    return res.json();
};
