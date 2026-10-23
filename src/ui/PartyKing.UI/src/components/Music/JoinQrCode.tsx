import { ReactNode, useEffect, useState } from 'react';
import { Dialog, DialogContent, DialogTitle } from '@mui/material';
import { QRCodeSVG } from 'qrcode.react';
import { sessionPath } from '@/router/constants';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';

// The QR is swapped for a fresh one this long before its token expires
const REFRESH_MARGIN_MS = 30_000;
const MIN_REFRESH_MS = 5_000;

type Props = {
  sessionId: string;
};

/**
 * "Show QR code" button for the host and admins. The dialog shows a QR that lets guests join without the password;
 * its token is short-lived, so it is replaced before it expires for as long as the dialog stays open.
 */
export const JoinQrCode = ({ sessionId }: Props): ReactNode => {
  const [open, setOpen] = useState(false);
  const [joinUrl, setJoinUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;

    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const load = async () => {
      try {
        const { token, expiresAt } = await sessionsApi.createJoinToken(sessionId);
        if (cancelled) return;

        setJoinUrl(`${window.location.origin}${sessionPath(sessionId)}?join=${encodeURIComponent(token)}`);
        setError(null);

        const refreshIn = Math.max(new Date(expiresAt).getTime() - Date.now() - REFRESH_MARGIN_MS, MIN_REFRESH_MS);
        timer = setTimeout(() => void load(), refreshIn);
      } catch (e) {
        if (cancelled) return;
        setError((e as { message?: string })?.message ?? 'Could not create the QR code');
      }
    };

    void load();

    return () => {
      cancelled = true;
      clearTimeout(timer);
      setJoinUrl(null);
    };
  }, [open, sessionId]);

  return (
    <div className="px-2 pt-2">
      <button
        onClick={() => setOpen(true)}
        className="px-3 py-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer"
      >
        Show QR code
      </button>

      <Dialog open={open} onClose={() => setOpen(false)} aria-labelledby="join-qr-title">
        <DialogTitle id="join-qr-title">Scan to join without the password</DialogTitle>
        <DialogContent>
          {error && <p role="alert">{error}</p>}
          {joinUrl && (
            <div className="flex flex-col items-center gap-3">
              <div className="bg-white p-4">
                <QRCodeSVG value={joinUrl} size={288} title="QR code to join the session" />
              </div>
              <p className="text-sm">The code refreshes by itself. Close this window when everyone has joined.</p>
            </div>
          )}
          {!joinUrl && !error && <p>Creating the QR code…</p>}
          <button onClick={() => setOpen(false)} className="mt-3 text-sm underline cursor-pointer">
            Close
          </button>
        </DialogContent>
      </Dialog>
    </div>
  );
};
