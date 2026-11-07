import styles from "../sentinel.module.css";

const RADIUS = 52;
const CX = 70;
const CY = 70;
const START = -120;
const SWEEP = 240;

function polar(r: number, deg: number) {
  const rad = ((deg - 90) * Math.PI) / 180;
  return { x: CX + r * Math.cos(rad), y: CY + r * Math.sin(rad) };
}

function arc(startDeg: number, endDeg: number) {
  const s = polar(RADIUS, startDeg);
  const e = polar(RADIUS, endDeg);
  const large = endDeg - startDeg > 180 ? 1 : 0;
  return `M ${s.x} ${s.y} A ${RADIUS} ${RADIUS} 0 ${large} 1 ${e.x} ${e.y}`;
}

export function AlignmentGauge({ score }: { score: number | null }) {
  const pct = score !== null ? Math.round(score * 100) : null;
  const label = pct === null ? "No Data" : pct >= 70 ? "Aligned" : pct >= 40 ? "Uncertain" : "Misaligned";
  const tone = pct === null ? "none" : pct >= 70 ? "ok" : pct >= 40 ? "warn" : "critical";
  const filled = pct !== null ? (pct / 100) * SWEEP : 0;

  return (
    <div className={styles.gauge} data-tone={tone}>
      <svg width="160" height="112" viewBox="0 0 140 100" role="img" aria-label={`Alignment ${pct ?? "unknown"}%`}>
        <path
          d={arc(START, START + SWEEP)}
          className={styles.gaugeTrack}
          fill="none"
          strokeWidth="9"
          strokeLinecap="round"
        />
        {filled > 0 && (
          <path
            d={arc(START, START + filled)}
            className={styles.gaugeFill}
            fill="none"
            strokeWidth="9"
            strokeLinecap="round"
          />
        )}
        <text x={CX} y={CY - 2} textAnchor="middle" className={styles.gaugeValue}>
          {pct !== null ? `${pct}%` : "?"}
        </text>
        <text x={CX} y={CY + 16} textAnchor="middle" className={styles.gaugeLabel}>
          {label}
        </text>
      </svg>
      <p className={styles.gaugeCaption}>Average alignment of the last 20 decisions with a semantic check</p>
    </div>
  );
}
