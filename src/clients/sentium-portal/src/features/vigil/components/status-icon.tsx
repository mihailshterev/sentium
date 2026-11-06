import { AlertCircle, AlertTriangle, CheckCircle, XCircle } from "lucide-react";
import styles from "../vigil.module.css";
import type { ServiceStatus } from "@/features/vigil/types/serviceHealth";

const ICONS = {
  Healthy: CheckCircle,
  Degraded: AlertTriangle,
  Unhealthy: XCircle,
  Unknown: AlertCircle,
} as const;

export function StatusIcon({ status }: { status: ServiceStatus }) {
  const Icon = ICONS[status] ?? AlertCircle;
  return <Icon size={15} className={styles.statusIcon} data-status={status} aria-hidden="true" />;
}
