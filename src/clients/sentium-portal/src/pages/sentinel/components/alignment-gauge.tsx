import styles from "../sentinel.module.scss";

const AlignmentGauge = ({ score }: { score: number | null }) => {
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
  const totalArc = 240;
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
};

export default AlignmentGauge;
