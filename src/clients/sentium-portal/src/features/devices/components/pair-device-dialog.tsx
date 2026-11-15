import { useEffect, useState } from "react";
import { MonitorSmartphone } from "lucide-react";
import type { PairingCode } from "@/features/devices/types/device";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { StatusMessage } from "@/shared/ui/status-message";
import styles from "../devices.module.css";

interface PairDeviceDialogProps {
  open: boolean;
  pairingCode: PairingCode | null;
  isCreating: boolean;
  error: Error | null;
  onNewCode: () => void;
  onClose: () => void;
}

const formatCode = (code: string) => `${code.slice(0, 4)}-${code.slice(4)}`;

const formatRemaining = (ms: number) => {
  const totalSeconds = Math.max(0, Math.ceil(ms / 1000));
  return `${Math.floor(totalSeconds / 60)}:${String(totalSeconds % 60).padStart(2, "0")}`;
};

function useRemaining(expiresAt: string | undefined) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!expiresAt) {
      return;
    }
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [expiresAt]);

  return expiresAt ? new Date(expiresAt).getTime() - now : 0;
}

/** Shows the one-time code and the server address to type into Sentium Tray on the PC being paired. */
export function PairDeviceDialog({ open, pairingCode, isCreating, error, onNewCode, onClose }: PairDeviceDialogProps) {
  const remaining = useRemaining(open ? pairingCode?.expiresAt : undefined);
  const expired = pairingCode !== null && remaining <= 0;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Pair a device"
      description="Open Sentium Tray on the PC and type these two values. The code works once."
      icon={<MonitorSmartphone />}
      size="sm"
      footer={
        <>
          {expired && (
            <Button variant="secondary" onClick={onNewCode} loading={isCreating}>
              New code
            </Button>
          )}
          <Button variant="primary" onClick={onClose}>
            Done
          </Button>
        </>
      }
    >
      {error ? (
        <StatusMessage variant="error" message={error.message} />
      ) : (
        <dl className={styles.pairing}>
          <div>
            <dt>Server</dt>
            <dd className={styles.server} data-testid="pairing-server">
              {window.location.origin}
            </dd>
          </div>
          <div>
            <dt>Code</dt>
            <dd className={styles.code} data-testid="pairing-code" data-expired={expired || undefined}>
              {pairingCode ? formatCode(pairingCode.code) : "····-····"}
            </dd>
          </div>
          <p className={styles.expiry} role="status">
            {!pairingCode ? "Creating a code…" : expired ? "This code has expired." : `Expires in ${formatRemaining(remaining)}`}
          </p>
        </dl>
      )}
    </Dialog>
  );
}
