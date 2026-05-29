import { useNavigate } from "react-router";
import { TrendingUp } from "lucide-react";
import styles from "./dashboard.module.scss";
import useAgents from "../../hooks/useAgents";
import useWorkflows from "../../hooks/useWorkflows";
import useServiceHealth from "../../hooks/useServiceHealth";
import PageHeader from "../../components/ui/page-header";
import StatCardsRow from "./components/stat-cards-row";
import QuickAccessGrid from "./components/quick-access-grid";
import SystemModules from "./components/system-modules";
import ActivityFeed from "./components/activity-feed";
import SecurityRow from "./components/security-row";

const Dashboard = () => {
  const navigate = useNavigate();
  const { agents, isLoading: agentsLoading } = useAgents();
  const { workflows, isLoading: workflowsLoading } = useWorkflows();
  const { services: serviceHealth } = useServiceHealth();
  const loading = agentsLoading || workflowsLoading;

  return (
    <div className={styles.dashboardRoot}>
      <PageHeader
        title="Dashboard"
        subtitle="System overview and quick access"
        right={
          <div className={styles.headerRight}>
            <div className={styles.statusBadge}>
              <span className="status-dot"></span>
              All Systems Operational
            </div>
            <div className={styles.headerMeta}>
              <TrendingUp size={12} />
              <span>Updated just now</span>
            </div>
          </div>
        }
      />

      <div className={styles.content}>
        <StatCardsRow loading={loading} agentsCount={agents.length} workflowsCount={workflows.length} />

        <div className={styles.mainGrid}>
          <div className={styles.section}>
            <div className={styles.sectionHeader}>
              <span className={styles.sectionTitle}>Quick Access</span>
            </div>
            <QuickAccessGrid onNavigate={navigate} />
          </div>

          <div className={styles.rightColumn}>
            <div className={styles.section}>
              <div className={styles.sectionHeader}>
                <span className={styles.sectionTitle}>System Modules</span>
              </div>
              <SystemModules services={serviceHealth} />
            </div>

            <div className={styles.section}>
              <div className={styles.sectionHeader}>
                <span className={styles.sectionTitle}>Recent Activity</span>
                <span className={styles.sectionTag}>Live</span>
              </div>
              <ActivityFeed loading={loading} agents={agents} workflows={workflows} />
            </div>
          </div>
        </div>

        <SecurityRow onNavigate={navigate} />
      </div>
    </div>
  );
};

export default Dashboard;
