import { CheckCircle, Cpu, BrickWallShield, ShieldCheck, XCircle } from "lucide-react";
import styles from "../dashboard.module.scss";
import type { ServiceHealthStatus } from "../../../types/serviceHealth";

const MODULE_SERVICE_MAP: Record<string, string> = {
  "agent-runtime": "Agent Runtime",
  sentinel: "Sentinel",
  "identity-provider": "Identity",
};

const MODULES = [
  { key: "agent-runtime", label: "Agent Runtime", icon: Cpu, color: "green" },
  { key: "sentinel", label: "Sentinel", icon: BrickWallShield, color: "blue" },
  { key: "identity-provider", label: "Identity Provider", icon: ShieldCheck, color: "amber" },
];

interface SystemModulesProps {
  services: ServiceHealthStatus[];
}

const SystemModules = ({ services }: SystemModulesProps) => {
  const getModuleStatus = (moduleKey: string) => {
    const serviceName = MODULE_SERVICE_MAP[moduleKey];
    return services.find((s) => s.serviceName === serviceName);
  };

  return (
    <div className={styles.moduleList}>
      {MODULES.map((mod) => {
        const health = getModuleStatus(mod.key);
        const isUnhealthy = health?.status === "Unhealthy";
        const isUnknown = !health;
        return (
          <div key={mod.key} className={styles.moduleRow}>
            <div className={`${styles.moduleIcon} ${styles[`moduleIcon_${mod.color}`]}`}>
              <mod.icon size={14} />
            </div>
            <span className={styles.moduleLabel}>{mod.label}</span>
            <div className={`${styles.moduleStatus} ${isUnhealthy ? styles.moduleStatusUnhealthy : ""}`}>
              {isUnhealthy ? <XCircle size={13} /> : <CheckCircle size={13} />}
              <span>{isUnknown ? "Unknown" : isUnhealthy ? "Offline" : "Online"}</span>
            </div>
          </div>
        );
      })}
    </div>
  );
};

export default SystemModules;
