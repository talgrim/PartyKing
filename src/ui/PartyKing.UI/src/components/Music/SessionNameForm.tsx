import { FormEvent, ReactNode, useId, useState } from 'react';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { settingsButtonClass, settingsInputClass, settingsLabelClass, settingsMessageClass } from './settingsStyles';

type Props = {
  sessionId: string;
  currentName: string;
  /** Called after the name was saved. */
  onRenamed: () => void;
};

const MAX_NAME_LENGTH = 60;

/** Changes the name of the session. */
export const SessionNameForm = ({ sessionId, currentName, onRenamed }: Props): ReactNode => {
  const inputId = useId();
  const [name, setName] = useState(currentName);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const trimmed = name.trim();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setMessage(null);
    try {
      await sessionsApi.renameSession(sessionId, trimmed);
      setMessage('Name saved');
      onRenamed();
    } catch (error) {
      setMessage((error as { message?: string })?.message || 'Something went wrong');
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit}>
      <label htmlFor={inputId} className={settingsLabelClass}>
        Session name
      </label>
      <div className="flex gap-2">
        <input
          id={inputId}
          className={settingsInputClass}
          value={name}
          onChange={(e) => setName(e.target.value)}
          maxLength={MAX_NAME_LENGTH}
        />
        <button
          type="submit"
          disabled={busy || !trimmed || trimmed === currentName}
          className={settingsButtonClass}
        >
          Save name
        </button>
      </div>
      {message && <p className={`mt-2 ${settingsMessageClass}`}>{message}</p>}
    </form>
  );
};
