import { BrickWallShield, Loader, RefreshCw, ShieldCheck, Siren, Sliders, Zap } from "lucide-react";
import { useState } from "react";
import { useSentinelAudit, useSentinelStats } from "../../hooks/useSentinelAudit";
import { useSentinelSettings } from "../../hooks/useSentinelSettings";
import styles from "./sentinel.module.scss";
import PageHeader from "../../components/ui/page-header";
import AuditRow from "./components/audit-row";
import AlignmentGauge from "./components/alignment-gauge";
import SovereignControls from "./components/sovereign-controls";
import SentinelStats from "./components/sentinel-stats";

const Sentinel = () => {
  const { records, isLoading: auditLoading, refetch } = useSentinelAudit(100);
  const { stats } = useSentinelStats();
  const { settings, isUpdating, updateSettings } = useSentinelSettings();
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [autonomyDraft, setAutonomyDraft] = useState<number | null>(null);
  const displayAutonomy = autonomyDraft ?? settings?.autonomyLevel ?? 5;

  const toggle = (id: string) => setExpandedId((prev) => (prev === id ? null : id));

  const handleLockdown = () => {
    if (!settings) {
      return;
    }
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
    if (!settings) {
      return;
    }
    updateSettings({ semanticIntentCheckEnabled: !settings.semanticIntentCheckEnabled });
  };

  const denialRate = stats && stats.total > 0 ? Math.round((stats.denied / stats.total) * 100) : 0;

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<BrickWallShield size={18} className={styles.titleIcon} />}
        title="Sentinel"
        subtitle="Defense-in-Depth Policy Decision Point"
        right={
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
        }
      />

      <div className={styles.body}>
        <SentinelStats stats={stats} denialRate={denialRate} />

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
              {auditLoading && records.length === 0 && (
                <div className={styles.emptyState}>
                  <Loader size={15} className={styles.spinning} />
                  Loading audit records…
                </div>
              )}
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
              <SovereignControls
                settings={settings}
                isUpdating={isUpdating}
                displayAutonomy={displayAutonomy}
                onLockdown={handleLockdown}
                onSemanticToggle={handleSemanticToggle}
                onAutonomyChange={handleAutonomyChange}
                commitAutonomy={commitAutonomy}
              />
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
