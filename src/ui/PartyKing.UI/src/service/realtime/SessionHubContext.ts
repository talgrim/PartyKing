import { createContext, useContext } from 'react';
import { SessionHubHandle } from '@/service/realtime/useSessionMusicHub';

/** The hub connection of the session page, so views deeper in the tree can talk to it without owning a connection. */
export const SessionHubContext = createContext<SessionHubHandle>({ connection: undefined, connected: false });

export const useSessionHub = (): SessionHubHandle => useContext(SessionHubContext);
