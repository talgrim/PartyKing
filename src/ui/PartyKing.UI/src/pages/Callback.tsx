import {useEffect, useRef, useState} from "react";
import {useTranslation} from "react-i18next";
import {Link, useNavigate} from "react-router-dom";
import {sessionsApi} from "@/service/endpoints/sessions/sessionsApi";
import {ROUTE_PATHS, sessionPath} from "@/router/constants";
import {InfoMessage} from "@/components/common/InfoMessage";
import {ErrorMessage} from "@/components/common/ErrorMessage";

export const Callback = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const authorizeStarted = useRef(false);
  const [authorizeError, setAuthorizeError] = useState<string | null>(null);

  const searchParams = new URLSearchParams(window.location.search);
  const code = searchParams.get('code');
  const state = searchParams.get('state');
  const error = searchParams.get('error') ?? (code ? null : t('callback.missingCode'));
  const stateError = !error && !state ? t('callback.missingState') : null;

  useEffect(() => {
    // The authorization code is single-use, so guard against the StrictMode double effect
    if (error || stateError || !code || !state || authorizeStarted.current) {
      return;
    }
    authorizeStarted.current = true;

    sessionsApi.authenticate(code, state)
      .then(user => navigate(sessionPath(user.sessionId), {replace: true}))
      .catch((e: { message?: string }) => setAuthorizeError(e?.message ?? t('callback.loginFailed')));
  }, [code, state, error, stateError, navigate, t]);

  const message = error ?? stateError ?? authorizeError;

  if (message) {
    return (
      <div className="pt-8">
        <ErrorMessage message={message}/>
        <Link to={ROUTE_PATHS.HOME} className="text-amber-500 underline">{t('common.backToSessions')}</Link>
      </div>
    );
  }

  return <div className="pt-8"><InfoMessage message={t('callback.success')}/></div>;
};
