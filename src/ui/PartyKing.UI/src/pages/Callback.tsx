import {useEffect, useRef} from "react";
import {usersApi} from "@/service/endpoints/users/users.endpoints";
import {useNavigate} from "react-router-dom";
import {ROUTE_PATHS} from "@/router/constants";
import {InfoMessage} from "@/components/common/InfoMessage";
import {ErrorMessage} from "@/components/common/ErrorMessage";

export const Callback = () => {
  const navigate = useNavigate();
  const authorizeStarted = useRef(false);

  const searchParams = new URLSearchParams(window.location.search);
  const code = searchParams.get('code');
  const error = searchParams.get('error') ?? (code ? null : "Missing code");

  useEffect(() => {
    // The authorization code is single-use, so guard against the StrictMode double effect
    if (error || !code || authorizeStarted.current) {
      return;
    }
    authorizeStarted.current = true;

    usersApi.authorize({body: code})
      .then(_ => navigate(ROUTE_PATHS.MUSIC));
  }, [code, error, navigate]);

  if (error) {
    return <ErrorMessage message={error}/>;
  }

  return <InfoMessage message="Authorization successful. Redirecting to the music page"/>;
};
