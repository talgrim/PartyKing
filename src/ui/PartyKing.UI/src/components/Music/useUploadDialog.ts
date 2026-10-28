import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';

export const UPLOAD_PARAM = 'upload';

/**
 * Open state of the photo upload dialog, kept in the address (`?upload=1`) so that the Back button closes it and a
 * reload (the phone's camera app can make the browser reload the tab) brings it back.
 */
export const useUploadDialog = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();

  const open = searchParams.get(UPLOAD_PARAM) === '1';

  // A new history entry, so that Back closes the dialog instead of leaving the page
  const show = () =>
    setSearchParams(
      (params) => {
        params.set(UPLOAD_PARAM, '1');
        return params;
      },
      { state: { uploadDialog: true } },
    );

  const close = () => {
    if ((location.state as { uploadDialog?: boolean } | null)?.uploadDialog) {
      // We opened it ourselves: go back to the entry without it
      navigate(-1);
      return;
    }

    // Opened by a link or a reload: there is no entry of ours to go back to
    setSearchParams(
      (params) => {
        params.delete(UPLOAD_PARAM);
        return params;
      },
      { replace: true },
    );
  };

  return { open, show, close };
};
