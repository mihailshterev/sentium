import { useState } from "react";
import { RefreshCw, ShieldCheck, Siren, Sliders } from "lucide-react";
import { useSentinelAudit, useSentinelStats } from "@/features/sentinel/hooks/useSentinelAudit";
import { useSentinelSettings } from "@/features/sentinel/hooks/useSentinelSettings";
import { useOllamaModels } from "@/features/models/hooks/useOllamaModels";
import styles from "../sentinel.module.css";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { LoadMore } from "@/shared/ui/load-more";
import { Page } from "@/shared/ui/page";
import { AuditRow } from "../components/audit-row";
import { AlignmentGauge } from "../components/alignment-gauge";
import { SovereignControls } from "../components/sovereign-controls";
import { SentinelStats } from "../components/sentinel-stats";

const AUDIT_COLUMNS = 8;

export function SentinelView() {
  const { records, hasMore, loadMore, isLoadingMore, isLoading: auditLoading, refetch } = useSentinelAudit(20);
  const { stats } = useSentinelStats();
  const { settings, isUpdating, updateSettings } = useSentinelSettings();
  const { models } = useOllamaModels();
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [autonomyDraft, setAutonomyDraft] = useState<number | null>(null);
  const displayAutonomy = autonomyDraft ?? settings?.autonomyLevel ?? 5;

  const toggle = (id: string) => setExpandedId((prev) => (prev === id ? null : id));

  const commitAutonomy = () => {
    if (autonomyDraft !== null && autonomyDraft !== settings?.autonomyLevel) {
      updateSettings({ autonomyLevel: autonomyDraft });
    }
    setAutonomyDraft(null);
  };

  const denialRate = stats && stats.total > 0 ? Math.round((stats.denied / stats.total) * 100) : 0;

  const risks = [
    { label: "Low", value: stats?.lowRisk ?? 0, tone: "low" },
    { label: "Medium", value: stats?.mediumRisk ?? 0, tone: "medium" },
    { label: "High", value: stats?.highRisk ?? 0, tone: "high" },
    { label: "Critical", value: stats?.criticalRisk ?? 0, tone: "critical" },
  ];

  return (
    <Page
      title="Sentinel"
      subtitle="Defense-in-depth policy decision point"
      width="wide"
      actions={
        <>
          {settings?.lockdownMode && (
            <Badge tone="danger" dot className={styles.lockdown}>
              <Siren size={13} aria-hidden="true" />
              Lockdown active
            </Badge>
          )}
          <Button
            variant="secondary"
            icon={<RefreshCw size={15} className={auditLoading ? "animate-spin" : undefined} />}
            onClick={() => refetch()}
            disabled={auditLoading}
          >
            Refresh
          </Button>
        </>
      }
    >
      <SentinelStats stats={stats} denialRate={denialRate} />

      <div className={styles.mainGrid}>
        <Card padding="none" as="section" className={styles.auditCard}>
          <CardHeader
            title="Security Pulse"
            description="Every policy decision, newest first"
            actions={
              <Badge tone="success" dot>
                Live
              </Badge>
            }
          />

          <div className="data-table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Agent</th>
                  <th>Skill</th>
                  <th>Action</th>
                  <th>Decision</th>
                  <th>Risk</th>
                  <th>Alignment</th>
                  <th aria-label="Details" />
                </tr>
              </thead>
              <tbody>
                {auditLoading && records.length === 0 && (
                  <tr>
                    <td colSpan={AUDIT_COLUMNS} className={styles.emptyCell}>
                      Loading audit records…
                    </td>
                  </tr>
                )}
                {!auditLoading && records.length === 0 && (
                  <tr>
                    <td colSpan={AUDIT_COLUMNS} className={styles.emptyCell}>
                      No decisions recorded yet. Decisions appear here in real-time.
                    </td>
                  </tr>
                )}
                {records.map((r) => (
                  <AuditRow
                    key={r.id}
                    record={r}
                    expanded={expandedId === r.id}
                    onToggle={() => toggle(r.id)}
                    columns={AUDIT_COLUMNS}
                  />
                ))}
              </tbody>
            </table>
          </div>

          <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
        </Card>

        <div className={styles.sideColumn}>
          <Card padding="md" as="section">
            <CardHeader icon={<Sliders />} title="Sovereign Controls" />
            <SovereignControls
              settings={settings}
              isUpdating={isUpdating}
              displayAutonomy={displayAutonomy}
              models={models}
              onLockdown={() => settings && updateSettings({ lockdownMode: !settings.lockdownMode })}
              onSemanticToggle={() =>
                settings && updateSettings({ semanticIntentCheckEnabled: !settings.semanticIntentCheckEnabled })
              }
              onAutonomyChange={(e) => setAutonomyDraft(Number(e.target.value))}
              commitAutonomy={commitAutonomy}
              onIntentModelChange={(model) => updateSettings({ intentCheckModel: model })}
            />
          </Card>

          <Card padding="md" as="section">
            <CardHeader icon={<ShieldCheck />} title="Semantic Alignment" />
            <AlignmentGauge score={stats?.latestAlignmentScore ?? null} />

            <ul className={styles.riskBars}>
              {risks.map((r) => (
                <li key={r.label} className={styles.riskBar} data-tone={r.tone}>
                  <span className={styles.riskLabel}>{r.label}</span>
                  <span className={styles.riskTrack}>
                    <span
                      className={styles.riskFill}
                      style={{ width: stats?.total ? `${(r.value / stats.total) * 100}%` : "0%" }}
                    />
                  </span>
                  <span className={styles.riskCount}>{r.value}</span>
                </li>
              ))}
            </ul>
          </Card>
        </div>
      </div>
    </Page>
  );
}
