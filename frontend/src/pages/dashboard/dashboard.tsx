import { useNavigate } from "react-router";
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Bot,
  BotMessageSquare,
  BrickWallShield,
  CheckCircle,
  Cpu,
  GitBranch,
  Globe,
  Package,
  ShieldCheck,
  TrendingUp,
  View,
  Zap,
} from "lucide-react";
import styles from "./dashboard.module.scss";
import useAgents from "../../hooks/useAgents";
import useWorkflows from "../../hooks/useWorkflows";

const MODULES = [
  { key: "agent-runtime", label: "Agent Runtime", icon: Cpu, color: "green" },
  { key: "sentinel", label: "Sentinel", icon: BrickWallShield, color: "blue" },
  { key: "network-filter", label: "Network Filter", icon: Globe, color: "purple" },
  { key: "identity-provider", label: "Identity Provider", icon: ShieldCheck, color: "amber" },
];

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
    description: "Threat detection and security event monitoring",
    icon: BrickWallShield,
    color: "amber",
  },
  {
    to: "/watchdog",
    label: "Watchdog",
    description: "Network traffic inspection and analysis",
    icon: View,
    color: "red",
  },
];

const Dashboard = () => {
  const navigate = useNavigate();
  const { agents, isLoading: agentsLoading } = useAgents();
  const { workflows, isLoading: workflowsLoading } = useWorkflows();
  const loading = agentsLoading || workflowsLoading;

  return (
    <div className={styles.dashboardRoot}>
      <div className={styles.dashboardHeader}>
        <div className={styles.headerLeft}>
          <h1 className={styles.pageTitle}>Dashboard</h1>
          <p className={styles.pageSubtitle}>System overview and quick access</p>
        </div>
        <div className={styles.headerRight}>
          <div className={styles.statusBadge}>
            <span className="status-dot"></span>
            All Systems Operational
          </div>
          <div className={styles.headerMeta}>
            <TrendingUp size={12} />
            <span>Updated just now</span> {/* Placeholder - ideally would show actual last update time */}
          </div>
        </div>
      </div>

      <div className={styles.statsRow}>
        <div className={styles.statCard}>
          <div className={`${styles.statIcon} ${styles.iconGreen}`}>
            <Bot size={18} />
          </div>
          <div className={styles.statContent}>
            {loading ? (
              <span className={styles.skeletonValue} />
            ) : (
              <span className={styles.statValue}>{agents.length}</span>
            )}
            <span className={styles.statLabel}>Agents Configured</span>
          </div>
        </div>
        <div className={styles.statCard}>
          <div className={`${styles.statIcon} ${styles.iconBlue}`}>
            <GitBranch size={18} />
          </div>
          <div className={styles.statContent}>
            {loading ? (
              <span className={styles.skeletonValue} />
            ) : (
              <span className={styles.statValue}>{workflows.length}</span>
            )}
            <span className={styles.statLabel}>Workflows Defined</span>
          </div>
        </div>
        <div className={styles.statCard}>
          <div className={`${styles.statIcon} ${styles.iconAmber}`}>
            <AlertTriangle size={18} />
          </div>
          <div className={styles.statContent}>
            <span className={styles.statValue}>0</span>
            <span className={styles.statLabel}>Active Threats</span>
          </div>
          <span className={styles.statChip} data-variant="green">
            Clear
          </span>
        </div>
        <div className={styles.statCard}>
          <div className={`${styles.statIcon} ${styles.iconPurple}`}>
            <Zap size={18} />
          </div>
          <div className={styles.statContent}>
            <span className={styles.statValue}>Nominal</span>
            <span className={styles.statLabel}>System Health</span>
          </div>
          <span className={styles.statChip} data-variant="green">
            Healthy
          </span>
        </div>
      </div>

      <div className={styles.mainGrid}>
        <div className={styles.section}>
          <div className={styles.sectionHeader}>
            <span className={styles.sectionTitle}>Quick Access</span>
          </div>
          <div className={styles.quickGrid}>
            {QUICK_ACCESS.map((item) => (
              <button
                key={item.to}
                className={`${styles.quickCard} ${styles[`quick_${item.color}`]}`}
                onClick={() => navigate(item.to)}
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
        </div>

        <div className={styles.rightColumn}>
          <div className={styles.section}>
            <div className={styles.sectionHeader}>
              <span className={styles.sectionTitle}>System Modules</span>
            </div>
            <div className={styles.moduleList}>
              {MODULES.map((mod) => (
                <div key={mod.key} className={styles.moduleRow}>
                  <div className={`${styles.moduleIcon} ${styles[`moduleIcon_${mod.color}`]}`}>
                    <mod.icon size={14} />
                  </div>
                  <span className={styles.moduleLabel}>{mod.label}</span>
                  <div className={styles.moduleStatus}>
                    <CheckCircle size={13} />
                    <span>Online</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className={styles.section}>
            <div className={styles.sectionHeader}>
              <span className={styles.sectionTitle}>Recent Activity</span>
              <span className={styles.sectionTag}>Live</span>
            </div>
            <div className={styles.activityFeed}>
              {loading ? (
                [0, 1, 2].map((i) => (
                  <div key={i} className={styles.activityRow}>
                    <div className={`${styles.activityDot} ${styles.skeletonDot}`} />
                    <div className={styles.activityText}>
                      <span className={`${styles.skeletonLine} ${styles.skeletonLineLong}`} />
                      <span className={`${styles.skeletonLine} ${styles.skeletonLineShort}`} />
                    </div>
                  </div>
                ))
              ) : agents.length === 0 && workflows.length === 0 ? (
                <div className={styles.emptyState}>
                  <Activity size={20} />
                  <span>No recent activity</span>
                </div>
              ) : (
                <>
                  {workflows.slice(0, 3).map((wf) => (
                    <div key={wf.id} className={styles.activityRow}>
                      <div className={`${styles.activityDot} ${styles.dotBlue}`} />
                      <div className={styles.activityText}>
                        <span className={styles.activityMain}>
                          Workflow <strong>{wf.name}</strong> registered
                        </span>
                        <span className={styles.activityTime}>
                          {wf.agents.length} agent{wf.agents.length !== 1 ? "s" : ""}
                        </span>
                      </div>
                    </div>
                  ))}
                  {agents.slice(0, 3).map((ag) => (
                    <div key={ag.id} className={styles.activityRow}>
                      <div className={`${styles.activityDot} ${styles.dotGreen}`} />
                      <div className={styles.activityText}>
                        <span className={styles.activityMain}>
                          Agent <strong>{ag.name}</strong> available
                        </span>
                        <span className={styles.activityTime}>{ag.model}</span>
                      </div>
                    </div>
                  ))}
                </>
              )}
            </div>
          </div>
        </div>
      </div>

      <div className={styles.securityRow}>
        <div className={styles.securityCard}>
          <BrickWallShield size={16} />
          <div className={styles.securityCardContent}>
            <span className={styles.securityCardLabel}>Sentinel</span>
            <span className={styles.securityCardSub}>Threat monitoring active</span>
          </div>
          <button className={styles.securityCardBtn} onClick={() => navigate("/sentinel")}>
            View <ArrowRight size={12} />
          </button>
        </div>
        <div className={styles.securityCard}>
          <Globe size={16} />
          <div className={styles.securityCardContent}>
            <span className={styles.securityCardLabel}>Network Filter</span>
            <span className={styles.securityCardSub}>Packet inspection running</span>
          </div>
          <button className={styles.securityCardBtn} onClick={() => navigate("/watchdog")}>
            View <ArrowRight size={12} />
          </button>
        </div>
        <div className={styles.securityCard}>
          <Package size={16} />
          <div className={styles.securityCardContent}>
            <span className={styles.securityCardLabel}>Assets & Inventory</span>
            <span className={styles.securityCardSub}>Asset tracking placeholder</span>
          </div>
          <button className={styles.securityCardBtn} onClick={() => navigate("/inventory")}>
            View <ArrowRight size={12} />
          </button>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
