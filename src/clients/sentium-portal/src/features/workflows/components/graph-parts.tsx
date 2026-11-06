import styles from "../workflows.module.css";

const cx = (...classes: (string | false | null | undefined)[]) => classes.filter(Boolean).join(" ");

/** Connector between two graph rows: a hairline with a midpoint tick and a pulse travelling downwards. */
export function GraphEdge({ index }: { index: number }) {
  return (
    <span className={styles.edge} style={{ "--i": index } as React.CSSProperties} aria-hidden="true">
      <span className={styles.edgePulse} />
    </span>
  );
}

interface GraphRowProps {
  /** Technical annotation left of the node (step, model). */
  left?: React.ReactNode;
  /** The node disc (and anything anchored to it). */
  node: React.ReactNode;
  label: React.ReactNode;
  /** Descriptive annotation right of the node. */
  right?: React.ReactNode;
}

/** One level of the graph: annotations either side of a centred node with its label underneath. */
export function GraphRow({ left, node, label, right }: GraphRowProps) {
  return (
    <div className={styles.graphRow}>
      <div className={styles.graphLeft}>{left}</div>
      <div className={styles.graphCenter}>
        {node}
        <span className={styles.graphLabel}>{label}</span>
      </div>
      <div className={styles.graphRight}>{right}</div>
    </div>
  );
}

interface GraphTerminalProps {
  tone: "trigger" | "output";
  icon: React.ReactNode;
  tag: string;
  label: string;
  hint: string;
}

/** Fixed start / end node of the pipeline. */
export function GraphTerminal({ tone, icon, tag, label, hint }: GraphTerminalProps) {
  return (
    <GraphRow
      left={<span className={styles.graphTag}>{tag}</span>}
      node={
        <span className={cx(styles.graphNode, styles[`graphNode_${tone}`])} aria-hidden="true">
          {icon}
        </span>
      }
      label={label}
      right={<span className={styles.graphDesc}>{hint}</span>}
    />
  );
}
