import { Bot, GitBranch, AlertTriangle, Loader, Zap } from "lucide-react";
import styles from "../dashboard.module.scss";
import StatCard from "../../../components/ui/stat-card";

interface StatCardsRowProps {
  loading: boolean;
  agentsCount: number;
  workflowsCount: number;
}

const StatCardsRow = ({ loading, agentsCount, workflowsCount }: StatCardsRowProps) => (
  <div className={styles.statsRow}>
    <StatCard
      icon={loading ? <Loader size={18} className={styles.spinIcon} /> : <Bot size={18} />}
      value={loading ? "—" : agentsCount}
      label="Agents Configured"
      iconColor="green"
    />
    <StatCard
      icon={loading ? <Loader size={18} className={styles.spinIcon} /> : <GitBranch size={18} />}
      value={loading ? "—" : workflowsCount}
      label="Workflows Defined"
      iconColor="blue"
    />
    <StatCard
      icon={<AlertTriangle size={18} />}
      value={0}
      label="Policy Violations"
      iconColor="amber"
      chip="Clear"
      chipVariant="green"
    />
    <StatCard
      icon={<Zap size={18} />}
      value="Nominal"
      label="System Health"
      iconColor="purple"
      chip="Healthy"
      chipVariant="green"
    />
  </div>
);

export default StatCardsRow;
