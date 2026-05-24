import { Activity, ArrowRight, Bot, BotMessageSquare, BrickWallShield, GitBranch, View } from "lucide-react";
import styles from "../dashboard.module.scss";

const QUICK_ACCESS = [
  {
    to: "/orchestration",
    label: "Orchestration",
    description: "Launch and monitor multi-agent pipelines",
    icon: Activity,
    color: "green",
  },
  {
    to: "/assistant",
    label: "AI Assistant",
    description: "Interactive AI-powered analysis and queries",
    icon: BotMessageSquare,
    color: "blue",
  },
  {
    to: "/agents",
    label: "Agents",
    description: "Create and manage AI agent configurations",
    icon: Bot,
    color: "purple",
  },
  {
    to: "/workflows",
    label: "Workflows",
    description: "Design and run automated agent workflows",
    icon: GitBranch,
    color: "cyan",
  },
  {
    to: "/sentinel",
    label: "Sentinel",
    description: "Real-time security governance and policy enforcement",
    icon: BrickWallShield,
    color: "amber",
  },
  {
    to: "/watchdog",
    label: "Watchdog",
    description: "Service health monitoring and system diagnostics",
    icon: View,
    color: "red",
  },
];

interface QuickAccessGridProps {
  onNavigate: (to: string) => void;
}

const QuickAccessGrid = ({ onNavigate }: QuickAccessGridProps) => (
  <div className={styles.quickGrid}>
    {QUICK_ACCESS.map((item) => (
      <button
        key={item.to}
        className={`${styles.quickCard} ${styles[`quick_${item.color}`]}`}
        onClick={() => onNavigate(item.to)}
      >
        <div className={styles.quickCardIcon}>
          <item.icon size={20} />
        </div>
        <div className={styles.quickCardBody}>
          <span className={styles.quickCardLabel}>{item.label}</span>
          <span className={styles.quickCardDesc}>{item.description}</span>
        </div>
        <ArrowRight size={14} className={styles.quickCardArrow} />
      </button>
    ))}
  </div>
);

export default QuickAccessGrid;
