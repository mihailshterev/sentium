import { useNavigate } from "react-router";
import styles from "../dashboard.module.css";
import { useAgents } from "@/features/agents/hooks/useAgents";
import { useWorkflows } from "@/features/workflows/hooks/useWorkflows";
import { useWorkflowRuns } from "@/features/workflows/hooks/useWorkflowRuns";
import { useServiceHealth } from "@/features/vigil/hooks/useServiceHealth";
import { useSystemMetrics } from "@/features/vigil/hooks/useSystemMetrics";
import { useSentinelStats } from "@/features/sentinel/hooks/useSentinelAudit";
import { useModelCatalog } from "@/features/models/hooks/useModelCatalog";
import { useSchedulerJobs } from "@/features/scheduler/hooks/useScheduler";
import { useKnowledgeBaseStats } from "@/features/knowledge-base/hooks/useKnowledgeBaseStats";
import { ArrowRight, BotMessageSquare } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Page } from "@/shared/ui/page";
import { StatCardsRow } from "../components/stat-cards-row";
import { SystemMap } from "../components/system-map";
import { VitalsStrip } from "../components/vitals-strip";
import { WorkflowRunsFeed } from "../components/workflow-runs-feed";
import { ServiceHealthPanel } from "../components/service-health-panel";
import { SecurityPanel } from "../components/security-panel";

export function DashboardView() {
  const navigate = useNavigate();

  const { agents, isLoading: agentsLoading } = useAgents();
  const { workflows, isLoading: workflowsLoading } = useWorkflows();
  const { runs, isLoading: runsLoading } = useWorkflowRuns(7);
  const { services, isLoading: healthLoading } = useServiceHealth();
  const { metrics, isLoading: metricsLoading } = useSystemMetrics();
  const { stats: sentinelStats, isLoading: sentinelLoading } = useSentinelStats();
  const { models, isLoading: modelsLoading } = useModelCatalog();
  const { jobs, isLoading: jobsLoading } = useSchedulerJobs();
  const { collections, isLoading: kbLoading } = useKnowledgeBaseStats();

  const statsLoading =
    agentsLoading || workflowsLoading || modelsLoading || sentinelLoading || kbLoading || jobsLoading;

  const healthyCount = services.filter((s) => s.status === "Healthy").length;
  const allHealthy = services.length > 0 && healthyCount === services.length;

  const status =
    services.length === 0
      ? { tone: "neutral" as const, label: "Checking status…" }
      : allHealthy
        ? { tone: "success" as const, label: "All systems operational" }
        : { tone: "warning" as const, label: "Degraded services" };

  return (
    <Page
      className={styles.root}
      width="wide"
      title="Control Center"
      subtitle="Real-time system monitoring and intelligence"
    >
      <section className={styles.hero} aria-label="Overview">
        <div className={styles.heroText}>
          <Badge tone={status.tone} dot>
            {status.label}
          </Badge>
          <p className={styles.lead}>
            Local agents, workflows and knowledge, <span className={styles.leadMuted}>orchestrated under policy.</span>
          </p>
          <p className={styles.heroBody}>
            Every tool call passes Sentinel, every run is recorded. Ask the assistant, or hand a goal to a squad of
            agents and watch the plan unfold.
          </p>
          <div className={styles.heroActions}>
            <Button variant="primary" size="lg" icon={<BotMessageSquare />} onClick={() => navigate("/assistant")}>
              Open assistant
            </Button>
            <Button size="lg" variant="secondary" iconRight={<ArrowRight />} onClick={() => navigate("/orchestration")}>
              Orchestrate a goal
            </Button>
          </div>
          <StatCardsRow
            agentsCount={agents.length}
            workflowsCount={workflows.length}
            modelsCount={models.length}
            auditTotal={sentinelStats?.total ?? 0}
            kbCollections={collections.length}
            cronJobs={jobs.length}
            loading={statsLoading}
          />
        </div>
        <SystemMap className={styles.heroMap} services={services} agents={agents} />
      </section>

      <VitalsStrip metrics={metrics} services={services} loading={metricsLoading || healthLoading} />

      <div className={styles.mainGrid}>
        <WorkflowRunsFeed runs={runs} loading={runsLoading} onNavigate={navigate} />

        <div className={styles.sideColumn}>
          <ServiceHealthPanel services={services} loading={healthLoading} />
          <SecurityPanel stats={sentinelStats} loading={sentinelLoading} onNavigate={navigate} />
        </div>
      </div>
    </Page>
  );
}
