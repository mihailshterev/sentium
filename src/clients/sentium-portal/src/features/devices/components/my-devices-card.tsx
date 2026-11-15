import { useState } from "react";
import { Monitor, MonitorSmartphone, Plus, Trash2 } from "lucide-react";
import { useMyDevices } from "@/features/devices/hooks/useDevices";
import type { Device } from "@/features/devices/types/device";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { formatRelativeTime } from "@/shared/utils/formatters";
import { PairDeviceDialog } from "./pair-device-dialog";
import styles from "../devices.module.css";

/** The signed-in user's paired PCs: list, pair another, remove. */
export function MyDevicesCard({ className }: { className?: string }) {
  const { devices, isLoading, error, refresh, pairingCode, remove } = useMyDevices();
  const [isPairing, setIsPairing] = useState(false);
  const [toRemove, setToRemove] = useState<Device | null>(null);

  const startPairing = () => {
    pairingCode.reset();
    pairingCode.mutate();
    setIsPairing(true);
  };

  const finishPairing = () => {
    setIsPairing(false);
    void refresh();
  };

  const confirmRemove = () => {
    if (toRemove) {
      remove.mutate(toRemove.id);
    }
    setToRemove(null);
  };

  return (
    <Card padding="none" as="section" className={className} data-testid="my-devices">
      <CardHeader
        icon={<MonitorSmartphone />}
        title="My devices"
        description="PCs running Sentium Tray. They report their health to Vigil and show your notifications."
        actions={
          <Button size="sm" icon={<Plus />} onClick={startPairing} data-testid="pair-device">
            Pair a device
          </Button>
        }
      />

      {remove.error && <StatusMessage variant="error" message={remove.error.message} className={styles.message} />}

      {isLoading ? (
        <div className={styles.loading} aria-busy="true">
          <SkeletonText lines={2} />
        </div>
      ) : error ? (
        <StatusMessage variant="error" message="Your devices could not be loaded." className={styles.message} />
      ) : devices.length === 0 ? (
        <EmptyState
          compact
          icon={<Monitor />}
          title="No devices paired"
          hint="Install Sentium Tray on a PC, then pair it with a code from here."
        />
      ) : (
        <ul className={styles.list}>
          {devices.map((device) => (
            <li key={device.id} className={styles.item} data-testid="device-item">
              <Monitor size={18} className={styles.itemIcon} aria-hidden="true" />
              <div className={styles.itemText}>
                <span className={styles.itemName}>{device.name}</span>
                <span className={styles.itemMeta}>
                  {device.lastSeenAt ? `Last seen ${formatRelativeTime(device.lastSeenAt)}` : "Never connected"}
                  {device.appVersion && ` · v${device.appVersion}`}
                </span>
              </div>
              <Button
                variant="ghost"
                size="sm"
                iconOnly
                icon={<Trash2 />}
                aria-label={`Remove ${device.name}`}
                onClick={() => setToRemove(device)}
              />
            </li>
          ))}
        </ul>
      )}

      <PairDeviceDialog
        open={isPairing}
        pairingCode={pairingCode.data ?? null}
        isCreating={pairingCode.isPending}
        error={pairingCode.error}
        onNewCode={() => pairingCode.mutate()}
        onClose={finishPairing}
      />

      <ConfirmDialog
        open={toRemove !== null}
        variant="danger"
        title="Remove device"
        description={`"${toRemove?.name ?? ""}" will stop reporting and showing your notifications. To use it again, pair it again.`}
        confirmLabel="Remove"
        onConfirm={confirmRemove}
        onCancel={() => setToRemove(null)}
      />
    </Card>
  );
}
