import { ReactNode } from 'react';
import { Navigate, useParams } from 'react-router-dom';
import { sessionPath } from '@/router/constants';
import { UPLOAD_PARAM } from '@/components/Music/useUploadDialog';

/** Old upload page address: the uploader is a dialog on the session page now, so existing links end up there. */
export const UploadRedirect = (): ReactNode => {
  const { sessionId = '' } = useParams();

  return <Navigate to={`${sessionPath(sessionId)}?${UPLOAD_PARAM}=1`} replace />;
};
