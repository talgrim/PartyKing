import useSWR from 'swr';
import { sessionsApi } from './sessionsApi';
import { ViewerRoleDto } from './types';

const NO_ROLE: ViewerRoleDto = { isHost: false, isAdmin: false };

/** What the viewer may do in the session: only the host and admins get the player controls. */
export const useViewerRole = (sessionId: string): ViewerRoleDto => {
  const { data } = useSWR(['viewer-role', sessionId], () => sessionsApi.getViewerRole(sessionId), {
    shouldRetryOnError: false,
    // The role of a viewer hardly ever changes, so a tab coming back into focus needn't ask again
    revalidateOnFocus: false,
  });

  return data ?? NO_ROLE;
};
