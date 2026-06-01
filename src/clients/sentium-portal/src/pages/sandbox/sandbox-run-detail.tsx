import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, Code2, File, FileText, Loader, RefreshCw, Terminal } from "lucide-react";
import styles from "./sandbox.module.scss";
import { useSandboxExecution } from "../../hooks/useSandboxExecution";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import Tabs, { type TabItem } from "../../components/ui/tabs";
import MetadataBadges from "./components/metadata-badges";
import TerminalOutput from "./components/terminal-output";
import ArtifactCard from "./components/artifact-card";
import RunSummary from "./components/run-summary";
import RunScript from "./components/run-script";
import RunStatusBadge from "./components/run-status-badge";

type TabId = "summary" | "script" | "output" | "artifacts";

const SandboxRunDetail = () => {
  const { jobId } = useParams<{ jobId: string }>();
  const navigate = useNavigate();
  const { execution, isLoading, isFetching, refetch } = useSandboxExecution(jobId);
  const [activeTab, setActiveTab] = useState<TabId>("summary");

  const backButton = (
    <button className={styles.backBtn} onClick={() => navigate("/sandbox")} title="Back to runs">
      <ArrowLeft size={15} />
    </button>
  );

  if (isLoading) {
    return (
      <div className={styles.root}>
        <PageHeader icon={backButton} title="Sandbox Run" subtitle="Loading…" />
        <div className={styles.body}>
          <div className={styles.runsEmpty}>
            <Loader size={20} className={styles.spinning} />
          </div>
        </div>
      </div>
    );
  }

  if (!execution) {
    return (
      <div className={styles.root}>
        <PageHeader icon={backButton} title="Sandbox Run" subtitle="Not found" />
        <div className={styles.body}>
          <EmptyState
            icon={<Terminal size={32} />}
            title="Run not found"
            hint="This execution may have been evicted, or the link is invalid."
            action={
              <button className={styles.refreshBtn} onClick={() => navigate("/sandbox")}>
                <ArrowLeft size={13} /> Back to runs
              </button>
            }
          />
        </div>
      </div>
    );
  }

  const tabs: TabItem[] = [
    { id: "summary", label: "Summary", icon: <Code2 size={13} /> },
    { id: "script", label: "Script", icon: <FileText size={13} /> },
    { id: "output", label: "Output", icon: <Terminal size={13} /> },
    { id: "artifacts", label: "Artifacts", icon: <File size={13} />, count: execution.artifacts.length },
  ];

  return (
    <div className={styles.root}>
      <PageHeader
        icon={backButton}
        title={execution.agentId}
        subtitle={`Run ${execution.jobId.slice(0, 8)}…`}
        right={
          <div className={styles.headerActions}>
            <RunStatusBadge entry={execution} size={12} />
            <button className={styles.refreshBtn} onClick={() => refetch()} disabled={isFetching}>
              <RefreshCw size={13} className={isFetching ? styles.spinning : undefined} />
              Refresh
            </button>
          </div>
        }
      />

      <div className={styles.body}>
        <div className={styles.card}>
          <MetadataBadges entry={execution} />
          <Tabs
            tabs={tabs}
            active={activeTab}
            onChange={(id) => setActiveTab(id as TabId)}
            className={styles.detailTabs}
          />

          <div className={styles.tabPanel}>
            {activeTab === "summary" && <RunSummary entry={execution} />}
            {activeTab === "script" && <RunScript entry={execution} />}
            {activeTab === "output" && (
              <div className={styles.tabPanelPadded}>
                <TerminalOutput entry={execution} />
              </div>
            )}
            {activeTab === "artifacts" &&
              (execution.artifacts.length === 0 ? (
                <div className={styles.tabPanelPadded}>
                  <EmptyState icon={<File size={28} />} title="No artifacts produced by this run" />
                </div>
              ) : (
                <div className={styles.artifactsBody}>
                  <div className={styles.artifactsGrid}>
                    {execution.artifacts.map((a) => (
                      <ArtifactCard key={a.blobUri} artifact={a} />
                    ))}
                  </div>
                </div>
              ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default SandboxRunDetail;
