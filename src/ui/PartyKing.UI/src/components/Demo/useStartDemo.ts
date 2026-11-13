import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { sessionPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { demoApi } from '@/service/endpoints/demo/demoApi';

/** Starts a demo session of the visitor's own and opens it. */
export const useStartDemo = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const start = async () => {
    setStarting(true);
    setError(null);
    try {
      const started = await demoApi.start();
      navigate(sessionPath(started.sessionId));
    } catch (e) {
      const status = (e as { status?: number } | undefined)?.status;
      if (status === HTTP_STATUS_CODE.TOO_MANY_REQUESTS) setError(t('demo.errors.tooMany'));
      else if (status === HTTP_STATUS_CODE.CONFLICT) setError(t('demo.errors.full'));
      else setError(t('demo.errors.failed'));
    } finally {
      setStarting(false);
    }
  };

  return { start, starting, error };
};
