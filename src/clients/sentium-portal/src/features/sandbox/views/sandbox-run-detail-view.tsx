import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, Code2, File, FileText, RefreshCw, Terminal } from "lucide-react";
import styles from "../sandbox.module.css";
import { useSandboxExecution } from "@/features/sandbox/hooks/useSandboxExecution";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { Tabs, type TabItem } from "@/shared/ui/tabs";
import { ArtifactCard } from "../components/artifact-card";
import { MetadataBadges } from "../components/metadata-badges";
import { RunScript } from "../components/run-script";
import { RunStatusBadge } from "../components/run-status-badge";
import { RunSummary } from "../components/run-summary";
import { TerminalOutput } from "../components/terminal-output";

type TabId = "summary" | "script" | "output" | "artifacts";

export function SandboxRunDetailView() {
  const { jobId } = useParams<{ jobId: string }>();
  const navigate = useNavigate();
  const { execution, isLoading, isFetching, refetch } = useSandboxExecution(jobId);
  const [activeTab, setActiveTab] = useState<TabId>("summary");

  const backButton = (
    <Button
      variant="secondary"
      size="sm"
      iconOnly
      icon={<ArrowLeft size={16} />}
      onClick={() => navigate("/sandbox")}
      title="Back to runs"
    />
  );

  if (isLoading) {
    return (
      <Page title="Sandbox Run" subtitle="Loading…" leading={backButton}>
        <Card padding="lg" aria-busy="true">
          <SkeletonText lines={5} />
        </Card>
      </Page>
    );
  }

  if (!execution) {
    return (
      <Page title="Sandbox Run" subtitle="Not found" leading={backButton}>
        <Card>
          <EmptyState
            icon={<Terminal />}
            title="Run not found"
            hint="This execution may have been evicted, or the link is invalid."
            action={
              <Button variant="secondary" icon={<ArrowLeft size={15} />} onClick={() => navigate("/sandbox")}>
                Back to runs
              </Button>
            }
          />
        </Card>
      </Page>
    );
  }

  const tabs: TabItem[] = [
    { id: "summary", label: "Summary", icon: <Code2 /> },
    { id: "script", label: "Script", icon: <FileText /> },
    { id: "output", label: "Output", icon: <Terminal /> },
    { id: "artifacts", label: "Artifacts", icon: <File />, count: execution.artifacts.length },
  ];

  return (
    <Page
      title={execution.agentId}
      subtitle={`Run ${execution.jobId.slice(0, 8)}…`}
      leading={backButton}
      actions={
        <>
          <RunStatusBadge entry={execution} />
          <Button
            variant="secondary"
            icon={<RefreshCw size={15} className={isFetching ? "animate-spin" : undefined} />}
            onClick={() => refetch()}
            disabled={isFetching}
          >
            Refresh
          </Button>
        </>
      }
    >
      <MetadataBadges entry={execution} />
      <Tabs tabs={tabs} active={activeTab} onChange={(id) => setActiveTab(id as TabId)} label="Run details" />

      <Card padding="md" as="section">
        {activeTab === "summary" && <RunSummary entry={execution} />}
        {activeTab === "script" && <RunScript entry={execution} />}
        {activeTab === "output" && <TerminalOutput entry={execution} />}
        {activeTab === "artifacts" &&
          (execution.artifacts.length === 0 ? (
            <EmptyState compact icon={<File />} title="No artifacts produced by this run" />
          ) : (
            <div className={styles.artifactsGrid}>
              {execution.artifacts.map((a) => (
                <ArtifactCard key={a.blobUri} artifact={a} />
              ))}
            </div>
          ))}
      </Card>
    </Page>
  );
}
