import {usersApi} from "@/service/endpoints/users/users.endpoints";
import {useNavigate} from "react-router-dom";
import {ROUTE_PATHS} from "@/router/constants";
import {InfoMessage} from "@/components/common/InfoMessage";
import {ErrorMessage} from "@/components/common/ErrorMessage";

export const Callback = () => {
  const navigate = useNavigate();

  const searchParams = new URLSearchParams(window.location.search);
  let error = searchParams.get('error');
  const code = searchParams.get('code');

  if (!error && !code) {
    error = "Missing code";
  }

  if (error) {
    return <ErrorMessage message={error}/>;
  }

  const authorizeHandler = async (code: string) => {
    return await usersApi.authorize({
      body: code,
    });
  }

  // @ts-ignore
  authorizeHandler(code)
    .then(_ => navigate(ROUTE_PATHS.HOME));

  return <InfoMessage message="Authorization successful. Redirecting to Dashboard"/>;
};
