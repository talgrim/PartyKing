import {useEffect, useRef, useState} from "react";
import {Link, useNavigate} from "react-router-dom";
import {sessionsApi} from "@/service/endpoints/sessions/sessionsApi";
import {ROUTE_PATHS, sessionPath} from "@/router/constants";
import {InfoMessage} from "@/components/common/InfoMessage";
import {ErrorMessage} from "@/components/common/ErrorMessage";

export const Callback = () => {
  const navigate = useNavigate();
  const authorizeStarted = useRef(false);
  const [authorizeError, setAuthorizeError] = useState<string | null>(null);

  const searchParams = new URLSearchParams(window.location.search);
  const code = searchParams.get('code');
  const state = searchParams.get('state');
  const error = searchParams.get('error') ?? (code ? null : "Missing code");
  const stateError = !error && !state ? "Missing state" : null;

  useEffect(() => {
    // The authorization code is single-use, so guard against the StrictMode double effect
    if (error || stateError || !code || !state || authorizeStarted.current) {
      return;
    }
    authorizeStarted.current = true;

    sessionsApi.authenticate(code, state)
      .then(user => navigate(sessionPath(user.sessionId), {replace: true}))
      .catch((e: { message?: string }) => setAuthorizeError(e?.message ?? "Could not log in"));
  }, [code, state, error, stateError, navigate]);

  const message = error ?? stateError ?? authorizeError;

  if (message) {
    return (
      <div className="pt-8">
        <ErrorMessage message={message}/>
        <Link to={ROUTE_PATHS.HOME} className="text-amber-500 underline">Back to the sessions</Link>
      </div>
    );
  }

  return <div className="pt-8"><InfoMessage message="Authorization successful. Redirecting to the session"/></div>;
};
