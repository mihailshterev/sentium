import {
  AlertTriangle,
  BrickWallShield,
  CheckCircle,
  ChevronDown,
  ChevronUp,
  RefreshCw,
  Shield,
  ShieldAlert,
  ShieldCheck,
  ShieldOff,
  Siren,
  Sliders,
  Zap,
} from "lucide-react";
import { useState } from "react";
import { useSentinelAudit, useSentinelStats } from "../../hooks/useSentinelAudit";
import { useSentinelSettings } from "../../hooks/useSentinelSettings";
import type { AuditRecord, PolicyRiskLevel } from "../../types/sentinel";
import styles from "./sentinel.module.scss";

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

function RiskBadge({ risk }: { risk: PolicyRiskLevel }) {
  const cls = {
    Low: styles.riskLow,
    Medium: styles.riskMedium,
    High: styles.riskHigh,
    Critical: styles.riskCritical,
  }[risk];
  return <span className={`${styles.riskBadge} ${cls}`}>{risk}</span>;
}

function EffectBadge({ allowed, effect }: { allowed: boolean; effect: string }) {
  if (allowed) {
    return (
      <span className={`${styles.effectBadge} ${styles.effectAllow}`}>
        <CheckCircle size={11} /> Allow
      </span>
    );
  }
  if (effect === "DenyWithAlert") {
    return (
      <span className={`${styles.effectBadge} ${styles.effectAlert}`}>
        <Siren size={11} /> Alert
      </span>
    );
  }
  return (
    <span className={`${styles.effectBadge} ${styles.effectDeny}`}>
      <ShieldOff size={11} /> Deny
    </span>
  );
}

function AlignmentBadge({ verdict }: { verdict: string | null }) {
  if (!verdict) return <span className={styles.alignNone}>—</span>;
  const cls =
    verdict === "Aligned" ? styles.alignGood : verdict === "Misaligned" ? styles.alignBad : styles.alignNeutral;
  return <span className={`${styles.alignBadge} ${cls}`}>{verdict}</span>;
}

function AuditRow({ record, expanded, onToggle }: { record: AuditRecord; expanded: boolean; onToggle: () => void }) {
  return (
    <>
      <div
        className={`${styles.auditRow} ${!record.allowed ? styles.auditRowDenied : ""} ${expanded ? styles.auditRowExpanded : ""}`}
        onClick={onToggle}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === "Enter" && onToggle()}
      >
        <span className={styles.auditTime}>{formatTime(record.timestamp)}</span>
        <span className={styles.auditAgent} title={record.agentId}>
          {record.agentId}
        </span>
        <span className={styles.auditSkill} title={record.skillName}>
          {record.skillName || "—"}
        </span>
        <span className={styles.auditAction}>{record.action}</span>
        <EffectBadge allowed={record.allowed} effect={record.effect} />
        <RiskBadge risk={record.risk} />
        <AlignmentBadge verdict={record.alignmentVerdict} />
        <span className={styles.auditChevron}>{expanded ? <ChevronUp size={13} /> : <ChevronDown size={13} />}</span>
      </div>
      {expanded && (
        <div className={styles.auditDetail}>
          <div className={styles.auditDetailGrid}>
            <div>
              <span className={styles.auditDetailLabel}>Resource</span>
              <span className={styles.auditDetailValue}>
                {record.resourceType} / {record.resourceId}
              </span>
            </div>
            <div>
              <span className={styles.auditDetailLabel}>Policies Triggered</span>
              <span className={styles.auditDetailValue}>
                {record.triggeredPolicies.length > 0 ? record.triggeredPolicies.join(", ") : "None"}
              </span>
            </div>
            <div>
              <span className={styles.auditDetailLabel}>Eval Duration</span>
              <span className={styles.auditDetailValue}>{record.evaluationDurationMs}ms</span>
            </div>
            <div>
              <span className={styles.auditDetailLabel}>Correlation ID</span>
              <span className={`${styles.auditDetailValue} ${styles.mono}`}>{record.correlationId || "—"}</span>
            </div>
          </div>
          <div className={styles.auditReason}>
            <span className={styles.auditDetailLabel}>Reason</span>
            <p className={styles.auditReasonText}>{record.reason}</p>
          </div>
        </div>
      )}
    </>
  );
}

function AlignmentGauge({ score }: { score: number | null }) {
  const pct = score !== null ? Math.round(score * 100) : null;
  const label = pct === null ? "No Data" : pct >= 70 ? "Aligned" : pct >= 40 ? "Uncertain" : "Misaligned";
  const color =
    pct === null
      ? "var(--text-dim)"
      : pct >= 70
        ? "var(--accent-green)"
        : pct >= 40
          ? "var(--accent-amber)"
          : "var(--accent-red)";

  const radius = 52;
  const cx = 70;
  const cy = 70;
  const startAngle = -210;
  const totalArc = 240; // degrees
  const filled = pct !== null ? (pct / 100) * totalArc : 0;

  function polarToXY(cx: number, cy: number, r: number, deg: number) {
    const rad = ((deg - 90) * Math.PI) / 180;
    return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
  }

  function arcPath(cx: number, cy: number, r: number, startDeg: number, endDeg: number) {
    const s = polarToXY(cx, cy, r, startDeg);
    const e = polarToXY(cx, cy, r, endDeg);
    const large = endDeg - startDeg > 180 ? 1 : 0;
    return `M ${s.x} ${s.y} A ${r} ${r} 0 ${large} 1 ${e.x} ${e.y}`;
  }

  const trackPath = arcPath(cx, cy, radius, startAngle, startAngle + totalArc);
  const fillPath = filled > 0 ? arcPath(cx, cy, radius, startAngle, startAngle + filled) : null;

  return (
    <div className={styles.gaugeWrap}>
      <svg width="140" height="100" viewBox="0 0 140 100">
        <path d={trackPath} fill="none" stroke="var(--border-color)" strokeWidth="10" strokeLinecap="round" />
        {fillPath && <path d={fillPath} fill="none" stroke={color} strokeWidth="10" strokeLinecap="round" />}
        <text
          x={cx}
          y={cy - 4}
          textAnchor="middle"
          fill={color}
          fontSize="20"
          fontWeight="700"
          fontFamily="Inter, sans-serif"
        >
          {pct !== null ? `${pct}%` : "?"}
        </text>
        <text
          x={cx}
          y={cy + 14}
          textAnchor="middle"
          fill="var(--text-muted)"
          fontSize="10"
          fontFamily="Inter, sans-serif"
        >
          {label}
        </text>
      </svg>
      <p className={styles.gaugeCaption}>Avg alignment of last 20 decisions with semantic check</p>
    </div>
  );
}

const Sentinel = () => {
  const { records, isLoading: auditLoading, refetch } = useSentinelAudit(100);
  const { stats } = useSentinelStats();
  const { settings, isUpdating, updateSettings } = useSentinelSettings();
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [autonomyDraft, setAutonomyDraft] = useState<number | null>(null);
  const displayAutonomy = autonomyDraft ?? settings?.autonomyLevel ?? 5;

  const toggle = (id: string) => setExpandedId((prev) => (prev === id ? null : id));

  const handleLockdown = () => {
    if (!settings) return;
    updateSettings({ lockdownMode: !settings.lockdownMode });
  };

  const handleAutonomyChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setAutonomyDraft(Number(e.target.value));
  };

  const commitAutonomy = () => {
    if (autonomyDraft !== null && autonomyDraft !== settings?.autonomyLevel) {
      updateSettings({ autonomyLevel: autonomyDraft });
    }
    setAutonomyDraft(null);
  };

  const handleSemanticToggle = () => {
    if (!settings) return;
    updateSettings({ semanticIntentCheckEnabled: !settings.semanticIntentCheckEnabled });
  };

  const denialRate = stats && stats.total > 0 ? Math.round((stats.denied / stats.total) * 100) : 0;

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <BrickWallShield size={18} className={styles.titleIcon} />
          <div>
            <h1 className={styles.pageTitle}>Sentinel</h1>
            <p className={styles.pageSubtitle}>
              Defence-in-Depth Policy Decision Point — real-time security governance
            </p>
          </div>
        </div>
        <div className={styles.headerActions}>
          {settings?.lockdownMode && (
            <div className={styles.lockdownBanner}>
              <Siren size={13} />
              LOCKDOWN ACTIVE
            </div>
          )}
          <button className={styles.refreshBtn} onClick={() => refetch()} disabled={auditLoading}>
            <RefreshCw size={13} className={auditLoading ? styles.spinning : undefined} />
            Refresh
          </button>
        </div>
      </div>

      <div className={styles.body}>
        <div className={styles.statsRow}>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconBlue}`}>
              <Shield size={16} />
            </div>
            <div>
              <span className={styles.statValue}>{stats?.total ?? "—"}</span>
              <span className={styles.statLabel}>Total Decisions</span>
            </div>
          </div>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconGreen}`}>
              <ShieldCheck size={16} />
            </div>
            <div>
              <span className={`${styles.statValue} ${styles.green}`}>{stats?.allowed ?? "—"}</span>
              <span className={styles.statLabel}>Allowed</span>
            </div>
          </div>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconRed}`}>
              <ShieldOff size={16} />
            </div>
            <div>
              <span className={`${styles.statValue} ${styles.red}`}>{stats?.denied ?? "—"}</span>
              <span className={styles.statLabel}>Denied</span>
            </div>
          </div>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconAmber}`}>
              <Siren size={16} />
            </div>
            <div>
              <span className={`${styles.statValue} ${stats?.alerts ? styles.amber : ""}`}>{stats?.alerts ?? "—"}</span>
              <span className={styles.statLabel}>Alerts</span>
            </div>
          </div>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconPurple}`}>
              <AlertTriangle size={16} />
            </div>
            <div>
              <span className={`${styles.statValue} ${denialRate > 20 ? styles.red : ""}`}>
                {stats ? `${denialRate}%` : "—"}
              </span>
              <span className={styles.statLabel}>Denial Rate</span>
            </div>
          </div>
        </div>

        <div className={styles.mainGrid}>
          <div className={styles.auditPanel}>
            <div className={styles.panelHeader}>
              <div className={styles.panelTitle}>
                <Zap size={14} />
                Security Pulse
              </div>
              <span className={styles.liveTag}>LIVE</span>
            </div>

            <div className={styles.auditTableHead}>
              <span>Time</span>
              <span>Agent</span>
              <span>Skill</span>
              <span>Action</span>
              <span>Decision</span>
              <span>Risk</span>
              <span>Alignment</span>
              <span />
            </div>

            <div className={styles.auditBody}>
              {auditLoading && records.length === 0 && <div className={styles.emptyState}>Loading audit records…</div>}
              {!auditLoading && records.length === 0 && (
                <div className={styles.emptyState}>No decisions recorded yet. Decisions appear here in real-time.</div>
              )}
              {records.map((r) => (
                <AuditRow key={r.id} record={r} expanded={expandedId === r.id} onToggle={() => toggle(r.id)} />
              ))}
            </div>
          </div>

          <div className={styles.rightCol}>
            <div className={styles.card}>
              <div className={styles.cardHeader}>
                <div className={styles.panelTitle}>
                  <Sliders size={14} />
                  Sovereign Controls
                </div>
              </div>
              <div className={styles.cardBody}>
                <div className={styles.toggleRow}>
                  <div className={styles.toggleInfo}>
                    <span className={styles.toggleLabel}>
                      <ShieldAlert size={13} />
                      Lockdown Mode
                    </span>
                    <p className={styles.toggleDesc}>Deny all non-Read agent actions immediately.</p>
                  </div>
                  <button
                    className={`${styles.toggle} ${settings?.lockdownMode ? styles.toggleOn : ""}`}
                    onClick={handleLockdown}
                    disabled={isUpdating || !settings}
                    aria-pressed={settings?.lockdownMode}
                  >
                    <span className={styles.toggleThumb} />
                  </button>
                </div>

                <div className={styles.toggleRow}>
                  <div className={styles.toggleInfo}>
                    <span className={styles.toggleLabel}>
                      <Shield size={13} />
                      Semantic Intent Check
                    </span>
                    <p className={styles.toggleDesc}>LLM verification of agent intent alignment.</p>
                  </div>
                  <button
                    className={`${styles.toggle} ${settings?.semanticIntentCheckEnabled ? styles.toggleOn : ""}`}
                    onClick={handleSemanticToggle}
                    disabled={isUpdating || !settings}
                    aria-pressed={settings?.semanticIntentCheckEnabled}
                  >
                    <span className={styles.toggleThumb} />
                  </button>
                </div>

                <div className={styles.sliderSection}>
                  <div className={styles.sliderHeader}>
                    <span className={styles.toggleLabel}>
                      <Zap size={13} />
                      AI Autonomy
                    </span>
                    <span className={styles.sliderValue}>
                      {displayAutonomy <= 2
                        ? "Max Security"
                        : displayAutonomy >= 9
                          ? "Max Autonomy"
                          : `Level ${displayAutonomy}`}
                    </span>
                  </div>
                  <div className={styles.sliderLabels}>
                    <span>High Security</span>
                    <span>High Autonomy</span>
                  </div>
                  <input
                    type="range"
                    min={1}
                    max={10}
                    step={1}
                    value={displayAutonomy}
                    onChange={handleAutonomyChange}
                    onMouseUp={commitAutonomy}
                    onTouchEnd={commitAutonomy}
                    onKeyUp={commitAutonomy}
                    disabled={isUpdating || !settings}
                    className={styles.slider}
                  />
                  <p className={styles.toggleDesc}>
                    {displayAutonomy <= 2
                      ? "Inconclusive intent checks treated as denials."
                      : displayAutonomy >= 9
                        ? "Semantic intent check is bypassed entirely."
                        : "Semantic checks run normally — inconclusive = allow."}
                  </p>
                </div>

                <div className={styles.rateLimitRow}>
                  <span className={styles.toggleLabel}>Rate Limit</span>
                  <span className={styles.rateLimitValue}>
                    {settings ? `${settings.rateLimitMaxRequests} req / ${settings.rateLimitWindowSeconds}s` : "—"}
                  </span>
                </div>
              </div>
            </div>

            <div className={styles.card}>
              <div className={styles.cardHeader}>
                <div className={styles.panelTitle}>
                  <ShieldCheck size={14} />
                  Semantic Alignment
                </div>
              </div>
              <div className={styles.cardBody}>
                <AlignmentGauge score={stats?.latestAlignmentScore ?? null} />

                <div className={styles.riskBreakdown}>
                  <div className={styles.riskBar}>
                    <span className={styles.riskBarLabel}>Low</span>
                    <div className={styles.riskBarTrack}>
                      <div
                        className={`${styles.riskBarFill} ${styles.riskBarLow}`}
                        style={{ width: stats?.total ? `${(stats.lowRisk / stats.total) * 100}%` : "0%" }}
                      />
                    </div>
                    <span className={styles.riskBarCount}>{stats?.lowRisk ?? 0}</span>
                  </div>
                  <div className={styles.riskBar}>
                    <span className={styles.riskBarLabel}>Medium</span>
                    <div className={styles.riskBarTrack}>
                      <div
                        className={`${styles.riskBarFill} ${styles.riskBarMed}`}
                        style={{ width: stats?.total ? `${(stats.mediumRisk / stats.total) * 100}%` : "0%" }}
                      />
                    </div>
                    <span className={styles.riskBarCount}>{stats?.mediumRisk ?? 0}</span>
                  </div>
                  <div className={styles.riskBar}>
                    <span className={styles.riskBarLabel}>High</span>
                    <div className={styles.riskBarTrack}>
                      <div
                        className={`${styles.riskBarFill} ${styles.riskBarHigh}`}
                        style={{ width: stats?.total ? `${(stats.highRisk / stats.total) * 100}%` : "0%" }}
                      />
                    </div>
                    <span className={styles.riskBarCount}>{stats?.highRisk ?? 0}</span>
                  </div>
                  <div className={styles.riskBar}>
                    <span className={styles.riskBarLabel}>Critical</span>
                    <div className={styles.riskBarTrack}>
                      <div
                        className={`${styles.riskBarFill} ${styles.riskBarCrit}`}
                        style={{ width: stats?.total ? `${(stats.criticalRisk / stats.total) * 100}%` : "0%" }}
                      />
                    </div>
                    <span className={styles.riskBarCount}>{stats?.criticalRisk ?? 0}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Sentinel;
