import { useState } from "react";
import {
  Terminal,
  RefreshCw,
  Download,
  ExternalLink,
  File,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Clock,
  Cpu,
  MemoryStick,
  Hash,
  ShieldOff,
  Activity,
  Code2,
  FileCode,
} from "lucide-react";
import styles from "./sandbox.module.scss";
import { useSandboxExecutions } from "../../hooks/useSandboxExecutions";
import { getArtifactUrl } from "../../services/sandbox.service";
import type { ArtifactDto, SandboxExecutionLog } from "../../types/sandbox";
import { formatBytesToMb, formatTimeHms } from "../../utils/formatters";

function isImageMime(mime: string) {
  return mime.startsWith("image/");
}

function ArtifactCard({ artifact }: { artifact: ArtifactDto }) {
  const isImage = isImageMime(artifact.mimeType);
  const shortName = artifact.fileName.split("/").pop() ?? artifact.fileName;
  const url = getArtifactUrl(artifact.downloadPath);
  return (
    <div className={styles.artifactCard}>
      {isImage ? (
        <a href={url} target="_blank" rel="noopener noreferrer">
          <img
            src={url}
            alt={shortName}
            className={styles.artifactThumb}
            onError={(e) => ((e.currentTarget as HTMLImageElement).style.display = "none")}
          />
        </a>
      ) : (
        <div className={styles.artifactIconWrap}>
          <File size={26} />
        </div>
      )}
      <div className={styles.artifactBody}>
        <span className={styles.artifactName} title={artifact.fileName}>
          {shortName}
        </span>
        <span className={styles.artifactMeta}>
          {artifact.mimeType} · {formatBytesToMb(artifact.sizeBytes)}
        </span>
      </div>
      <div className={styles.artifactActions}>
        <a href={url} download={shortName} className={styles.artifactDownloadBtn} rel="noopener noreferrer">
          <Download size={11} /> Download
        </a>
        {isImage && (
          <a href={url} target="_blank" rel="noopener noreferrer" className={styles.artifactViewBtn}>
            <ExternalLink size={11} />
          </a>
        )}
      </div>
    </div>
  );
}

function MetadataBadges({ entry }: { entry: SandboxExecutionLog }) {
  return (
    <div className={styles.badges}>
      {entry.policyDenied ? (
        <span className={`${styles.badge} ${styles.badgeDenied}`}>
          <ShieldOff size={11} /> Policy Denied
        </span>
      ) : entry.succeeded ? (
        <span className={`${styles.badge} ${styles.badgeSuccess}`}>
          <CheckCircle2 size={11} /> Succeeded
        </span>
      ) : (
        <span className={`${styles.badge} ${styles.badgeFail}`}>
          <XCircle size={11} /> Failed
        </span>
      )}
      {entry.timedOut && (
        <span className={`${styles.badge} ${styles.badgeTimeout}`}>
          <AlertTriangle size={11} /> Timed Out
        </span>
      )}
      <span className={`${styles.badge} ${styles.badgeExit}`}>
        <Hash size={11} /> exit {entry.exitCode}
      </span>
      <span className={`${styles.badge} ${styles.badgeDuration}`}>
        <Clock size={11} /> {entry.durationMs.toLocaleString()} ms
      </span>
      <span className={`${styles.badge} ${styles.badgeMemory}`}>
        <MemoryStick size={11} /> 256 MB
      </span>
      <span className={`${styles.badge} ${styles.badgeCpu}`}>
        <Cpu size={11} /> 0.5 vCPU
      </span>
    </div>
  );
}

function TerminalOutput({ entry }: { entry: SandboxExecutionLog }) {
  const stdoutLines = entry.output ? entry.output.split("\n") : [];
  const stderrLines = entry.error ? entry.error.split("\n") : [];
  const hasStdout = stdoutLines.some((l) => l.trim());
  const hasStderr = stderrLines.some((l) => l.trim());

  return (
    <div className={styles.terminalWrap}>
      <div className={styles.terminalHeader}>
        <div className={styles.termDots}>
          <span className={styles.termDot} />
          <span className={styles.termDot} />
          <span className={styles.termDot} />
        </div>
        stdout / stderr
      </div>
      <div className={styles.terminal}>
        {!hasStdout && !hasStderr && <span className={styles.termEmpty}>(no output)</span>}
        {hasStdout &&
          stdoutLines.map((line, i) => (
            <span key={`out-${i}`} className={styles.termLineStdout}>
              {line || "\u00A0"}
            </span>
          ))}
        {hasStderr && (
          <>
            {hasStdout && <span className={styles.termSectionLabel}>── stderr ──</span>}
            {stderrLines.map((line, i) => (
              <span key={`err-${i}`} className={styles.termLineStderr}>
                {line || "\u00A0"}
              </span>
            ))}
          </>
        )}
      </div>
    </div>
  );
}

function AuditContext({ entry }: { entry: SandboxExecutionLog }) {
  return (
    <div className={styles.auditBody}>
      {entry.policyDenied && (
        <div className={styles.policyDeniedBanner}>
          <ShieldOff size={14} style={{ flexShrink: 0, marginTop: 1 }} />
          <span>
            <strong>Execution was denied by Sentinel PDP.</strong>
            {entry.policyDenialReason ? ` ${entry.policyDenialReason}` : ""}
          </span>
        </div>
      )}

      <dl className={styles.auditMeta}>
        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Agent ID</dt>
          <dd className={styles.metaValue} title={entry.agentId}>
            {entry.agentId}
          </dd>
        </div>
        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Language</dt>
          <dd className={styles.metaValue}>{entry.language}</dd>
        </div>
        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Job ID</dt>
          <dd className={styles.metaValue} title={entry.jobId}>
            {entry.jobId.slice(0, 8)}…
          </dd>
        </div>
        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Executed At</dt>
          <dd className={styles.metaValue}>{new Date(entry.executedAt).toLocaleString("en-GB")}</dd>
        </div>
        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Correlation ID</dt>
          <dd className={styles.metaValue} title={entry.correlationId}>
            {entry.correlationId.slice(0, 8)}…
          </dd>
        </div>
        <div className={styles.metaItem}>
          <dt className={styles.metaLabel}>Sentinel Audit ID</dt>
          <dd className={styles.metaValue} title={entry.sentinelAuditId}>
            {entry.sentinelAuditId.slice(0, 8)}…
          </dd>
        </div>
        {entry.originalUserPrompt && (
          <div className={styles.metaItem} style={{ gridColumn: "1 / -1" }}>
            <dt className={styles.metaLabel}>User Prompt</dt>
            <dd className={styles.metaValueFull}>{entry.originalUserPrompt}</dd>
          </div>
        )}
      </dl>

      <hr className={styles.divider} />

      <div>
        <p className={styles.codeLabel}>Source Code</p>
        <div className={styles.codeViewer}>
          <pre>
            <code>{entry.code}</code>
          </pre>
        </div>
      </div>

      {entry.fileContext.length > 0 && (
        <>
          <hr className={styles.divider} />
          <div className={styles.fileContextSection}>
            <p className={styles.codeLabel}>File Context ({entry.fileContext.length})</p>
            {entry.fileContext.map((f) => (
              <div key={f.fileName} className={styles.fileContextItem}>
                <div className={styles.fileContextName}>
                  <FileCode size={11} />
                  {f.fileName}
                </div>
                <div className={styles.fileContextCode}>
                  <pre>
                    <code>{f.content}</code>
                  </pre>
                </div>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

const Sandbox = () => {
  const { executions, isLoading, refetch } = useSandboxExecutions(100);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const selected = selectedId ? (executions.find((e) => e.jobId === selectedId) ?? null) : (executions[0] ?? null);

  const total = executions.length;
  const succeeded = executions.filter((e) => e.succeeded).length;
  const failed = executions.filter((e) => !e.succeeded && !e.policyDenied).length;
  const denied = executions.filter((e) => e.policyDenied).length;

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Terminal size={18} className={styles.titleIcon} />
          <div>
            <h1 className={styles.pageTitle}>Sandbox Inspector</h1>
            <p className={styles.pageSubtitle}>Read-only audit view of agent-initiated code executions</p>
          </div>
        </div>
        <div className={styles.headerActions}>
          <div className={styles.liveTag}>
            <span className={styles.liveDot} />
            Live
          </div>
          <button className={styles.refreshBtn} onClick={() => refetch()} disabled={isLoading}>
            <RefreshCw size={13} className={isLoading ? styles.spinning : undefined} />
            Refresh
          </button>
        </div>
      </div>

      <div className={styles.body}>
        <div className={styles.statsRow}>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconBlue}`}>
              <Activity size={16} />
            </div>
            <div>
              <span className={styles.statValue}>{total}</span>
              <span className={styles.statLabel}>Total Runs</span>
            </div>
          </div>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconGreen}`}>
              <CheckCircle2 size={16} />
            </div>
            <div>
              <span className={`${styles.statValue} ${styles.green}`}>{succeeded}</span>
              <span className={styles.statLabel}>Succeeded</span>
            </div>
          </div>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconRed}`}>
              <XCircle size={16} />
            </div>
            <div>
              <span className={`${styles.statValue} ${styles.red}`}>{failed}</span>
              <span className={styles.statLabel}>Failed</span>
            </div>
          </div>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconAmber}`}>
              <ShieldOff size={16} />
            </div>
            <div>
              <span className={`${styles.statValue} ${denied > 0 ? styles.amber : ""}`}>{denied}</span>
              <span className={styles.statLabel}>Policy Denied</span>
            </div>
          </div>
        </div>

        <div className={styles.mainGrid}>
          <div className={styles.leftCol}>
            <div className={styles.card}>
              <div className={styles.panelHeader}>
                <span className={styles.panelTitle}>
                  <Activity size={14} /> Execution History
                </span>
              </div>
              <div className={styles.historyList}>
                {executions.length === 0 && !isLoading && (
                  <p className={styles.historyEmpty}>
                    No executions recorded yet. They appear here as agents run code.
                  </p>
                )}
                {executions.map((e) => {
                  const isActive = selected?.jobId === e.jobId;
                  const statusCls = e.policyDenied
                    ? styles.statusDenied
                    : e.succeeded
                      ? styles.statusOk
                      : styles.statusFail;
                  const statusLabel = e.policyDenied ? "Denied" : e.succeeded ? "OK" : "Fail";
                  return (
                    <div
                      key={e.jobId}
                      className={`${styles.historyRow} ${isActive ? styles.historyRowActive : ""}`}
                      onClick={() => setSelectedId(e.jobId)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={(ev) => ev.key === "Enter" && setSelectedId(e.jobId)}
                    >
                      <span className={styles.historyTime}>{formatTimeHms(e.executedAt)}</span>
                      <span className={styles.historyAgent}>{e.agentId}</span>
                      <span
                        className={`${styles.langChip} ${e.language === "Python" ? styles.langPython : styles.langNode}`}
                      >
                        {e.language}
                      </span>
                      <span className={`${styles.statusChip} ${statusCls}`}>{statusLabel}</span>
                    </div>
                  );
                })}
              </div>
            </div>

            {selected && (
              <div className={styles.card}>
                <div className={styles.panelHeader}>
                  <span className={styles.panelTitle}>
                    <Code2 size={14} /> Audit Context
                  </span>
                </div>
                <AuditContext entry={selected} />
              </div>
            )}
          </div>

          <div className={styles.rightCol}>
            {!selected ? (
              <div className={styles.emptyRight}>
                <Terminal size={38} className={styles.emptyIcon} />
                <span className={styles.emptyText}>Select an execution from the history to inspect its output</span>
              </div>
            ) : (
              <>
                <div className={styles.card}>
                  <MetadataBadges entry={selected} />
                  <TerminalOutput entry={selected} />
                </div>

                {selected.artifacts.length > 0 && (
                  <div className={styles.card}>
                    <div className={styles.panelHeader}>
                      <span className={styles.panelTitle}>
                        <File size={14} /> Artifacts
                      </span>
                      <span style={{ fontSize: "0.72rem", color: "var(--text-muted)" }}>
                        {selected.artifacts.length} file{selected.artifacts.length !== 1 ? "s" : ""}
                      </span>
                    </div>
                    <div className={styles.artifactsBody}>
                      <div className={styles.artifactsGrid}>
                        {selected.artifacts.map((a) => (
                          <ArtifactCard key={a.blobUri} artifact={a} />
                        ))}
                      </div>
                    </div>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Sandbox;
