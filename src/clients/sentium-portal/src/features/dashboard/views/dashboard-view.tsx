import { useNavigate } from "react-router";
import styles from "../dashboard.module.scss";
import useAgents from "@/features/agents/hooks/useAgents";
import useWorkflows from "@/features/workflows/hooks/useWorkflows";
import useWorkflowRuns from "@/features/workflows/hooks/useWorkflowRuns";
import useServiceHealth from "@/features/watchdog/hooks/useServiceHealth";
import useSystemMetrics from "@/features/watchdog/hooks/useSystemMetrics";
import { useSentinelStats } from "@/features/sentinel/hooks/useSentinelAudit";
import useOllamaModels from "@/features/models/hooks/useOllamaModels";
import { useSchedulerJobs } from "@/features/scheduler/hooks/useScheduler";
import { useKnowledgeBaseStats } from "@/features/knowledge-base/hooks/useKnowledgeBaseStats";
import PageHeader from "@/shared/ui/page-header";
import StatCardsRow from "../components/stat-cards-row";
import VitalsStrip from "../components/vitals-strip";
import WorkflowRunsFeed from "../components/workflow-runs-feed";
import ServiceHealthPanel from "../components/service-health-panel";
import SecurityPanel from "../components/security-panel";

const Dashboard = () => {
  const navigate = useNavigate();

  const { agents, isLoading: agentsLoading } = useAgents();
  const { workflows, isLoading: workflowsLoading } = useWorkflows();
  const { runs, isLoading: runsLoading } = useWorkflowRuns(7);
  const { services, isLoading: healthLoading } = useServiceHealth();
  const { metrics, isLoading: metricsLoading } = useSystemMetrics();
  const { stats: sentinelStats, isLoading: sentinelLoading } = useSentinelStats();
  const { models, isLoading: modelsLoading } = useOllamaModels();
  const { jobs, isLoading: jobsLoading } = useSchedulerJobs();
  const { collections, isLoading: kbLoading } = useKnowledgeBaseStats();

  const statsLoading =
    agentsLoading || workflowsLoading || modelsLoading || sentinelLoading || kbLoading || jobsLoading;

  const healthyCount = services.filter((s) => s.status === "Healthy").length;
  const allHealthy = services.length > 0 && healthyCount === services.length;

  return (
    <div className={styles.dashboardRoot}>
      <PageHeader
        title="Control Center"
        subtitle="Real-time system monitoring and intelligence"
        right={
          <div
            className={`${styles.statusBadge} ${!allHealthy && services.length > 0 ? styles.statusBadgeDegraded : ""}`}
          >
            <span className="status-dot" />
            {allHealthy
              ? "All Systems Operational"
              : services.length === 0
                ? "Checking Status..."
                : "Degraded Services"}
          </div>
        }
      />

      <div className={styles.content}>
        <StatCardsRow
          agentsCount={agents.length}
          workflowsCount={workflows.length}
          modelsCount={models.length}
          auditTotal={sentinelStats?.total ?? 0}
          kbCollections={collections.length}
          cronJobs={jobs.length}
          loading={statsLoading}
        />

        <VitalsStrip metrics={metrics} services={services} loading={metricsLoading || healthLoading} />

        <div className={styles.mainGrid}>
          <WorkflowRunsFeed runs={runs} loading={runsLoading} onNavigate={navigate} />

          <div className={styles.rightColumn}>
            <ServiceHealthPanel services={services} loading={healthLoading} />
            <SecurityPanel stats={sentinelStats} loading={sentinelLoading} onNavigate={navigate} />
          </div>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
