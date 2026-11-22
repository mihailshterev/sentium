import { Link, useNavigate } from "react-router";
import { ArrowRight, BotMessageSquare, Orbit } from "lucide-react";
import styles from "../dashboard.module.css";
import { AttentionPanel } from "../components/attention-panel";
import { ContinuePanel } from "../components/continue-panel";
import { KpiGrid } from "../components/kpi-grid";
import { StatusStrip } from "../components/status-strip";
import { SystemMap } from "../components/system-map";
import { WorkflowRunsFeed } from "../components/workflow-runs-feed";
import { useAttention } from "../hooks/useAttention";
import { useAgents } from "@/features/agents/hooks/useAgents";
import { useConversations } from "@/features/assistant/hooks/useConversations";
import { useKnowledgeBaseStats } from "@/features/knowledge-base/hooks/useKnowledgeBaseStats";
import { useModelCatalog } from "@/features/models/hooks/useModelCatalog";
import { useProfile } from "@/features/profile/hooks/useProfile";
import { useSchedulerJobs } from "@/features/scheduler/hooks/useScheduler";
import { useSentinelStats } from "@/features/sentinel/hooks/useSentinelAudit";
import { useServiceHealth } from "@/features/vigil/hooks/useServiceHealth";
import { useSystemMetrics } from "@/features/vigil/hooks/useSystemMetrics";
import { useWorkflowRuns } from "@/features/workflows/hooks/useWorkflowRuns";
import { useWorkflows } from "@/features/workflows/hooks/useWorkflows";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { Page } from "@/shared/ui/page";

const RECENT_RUNS = 7;

function greeting(firstName?: string | null): string {
  const hour = new Date().getHours();
  const part = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
  return firstName ? `${part}, ${firstName}` : part;
}

/** Home: what needs attention, where you left off, then the headline numbers and recent activity. */
export function DashboardView() {
  const navigate = useNavigate();
  const { profile } = useProfile();

  const attention = useAttention();
  const { conversations, isLoading: conversationsLoading } = useConversations();
  const { agents, isLoading: agentsLoading } = useAgents();
  const { workflows, isLoading: workflowsLoading } = useWorkflows();
  const { runs, isLoading: runsLoading } = useWorkflowRuns(RECENT_RUNS);
  const { services, isLoading: healthLoading } = useServiceHealth();
  const { metrics, isLoading: metricsLoading } = useSystemMetrics();
  const { stats, isLoading: sentinelLoading } = useSentinelStats();
  const { models, isLoading: modelsLoading } = useModelCatalog();
  const { jobs, isLoading: jobsLoading } = useSchedulerJobs();
  const { collections, isLoading: kbLoading } = useKnowledgeBaseStats();

  const kpisLoading =
    agentsLoading ||
    workflowsLoading ||
    runsLoading ||
    healthLoading ||
    metricsLoading ||
    sentinelLoading ||
    modelsLoading ||
    jobsLoading ||
    kbLoading;

  return (
    <Page
      className={styles.root}
      width="wide"
      title="Home"
      subtitle={greeting(profile?.firstName)}
      actions={
        <>
          <Button variant="secondary" icon={<Orbit />} onClick={() => navigate("/orchestration")}>
            Orchestrate a goal
          </Button>
          <Button variant="primary" icon={<BotMessageSquare />} onClick={() => navigate("/assistant")}>
            New conversation
          </Button>
        </>
      }
    >
      <StatusStrip items={attention.items} services={services} metrics={metrics} loading={attention.isLoading} />

      <div className={styles.topGrid}>
        <AttentionPanel items={attention.items} loading={attention.isLoading} />
        <ContinuePanel conversations={conversations} loading={conversationsLoading} />
      </div>

      <KpiGrid
        services={services}
        metrics={metrics}
        stats={stats}
        runs={runs}
        agentCount={agents.length}
        workflowCount={workflows.length}
        modelCount={models.length}
        collectionCount={collections.length}
        jobCount={jobs.length}
        loading={kpisLoading}
      />

      <div className={styles.bottomGrid}>
        <WorkflowRunsFeed runs={runs} loading={runsLoading} onNavigate={navigate} />
        <Card padding="none" as="section" aria-label="System topology" className={styles.mapCard}>
          <CardHeader
            title="Topology"
            actions={
              <Link to="/vigil" className={styles.headerLink}>
                Open Vigil <ArrowRight size={13} aria-hidden="true" />
              </Link>
            }
          />
          <SystemMap services={services} agents={agents} className={styles.map} />
        </Card>
      </div>
    </Page>
  );
}
