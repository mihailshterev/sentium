import { Bot, GitBranch, BrainCircuit, ShieldCheck, Library, Clock, Loader } from "lucide-react";
import styles from "../dashboard.module.scss";
import StatCard from "../../../components/ui/stat-card";

interface StatCardsRowProps {
  agentsCount: number;
  workflowsCount: number;
  modelsCount: number;
  auditTotal: number;
  kbCollections: number;
  cronJobs: number;
  loading: boolean;
}

const loadingIcon = <Loader size={18} className={styles.spinIcon} />;

const StatCardsRow = ({
  agentsCount,
  workflowsCount,
  modelsCount,
  auditTotal,
  kbCollections,
  cronJobs,
  loading,
}: StatCardsRowProps) => (
  <div className={styles.statsRow}>
    <StatCard
      icon={loading ? loadingIcon : <Bot size={18} />}
      value={loading ? "—" : agentsCount}
      label="Agents"
      iconColor="green"
    />
    <StatCard
      icon={loading ? loadingIcon : <GitBranch size={18} />}
      value={loading ? "—" : workflowsCount}
      label="Workflows"
      iconColor="blue"
    />
    <StatCard
      icon={loading ? loadingIcon : <BrainCircuit size={18} />}
      value={loading ? "—" : modelsCount}
      label="AI Models"
      iconColor="purple"
    />
    <StatCard
      icon={loading ? loadingIcon : <ShieldCheck size={18} />}
      value={loading ? "—" : auditTotal}
      label="Audit Events"
      iconColor="amber"
    />
    <StatCard
      icon={loading ? loadingIcon : <Library size={18} />}
      value={loading ? "—" : kbCollections}
      label="KB Collections"
      iconColor="cyan"
    />
    <StatCard
      icon={loading ? loadingIcon : <Clock size={18} />}
      value={loading ? "—" : cronJobs}
      label="Cron Jobs"
      iconColor="red"
    />
  </div>
);

export default StatCardsRow;
