import { useId } from "react";
import { LatticeCore } from "./lattice-core";
import {
  CX,
  CY,
  R_EDGE,
  R_INNER,
  R_ORB,
  R_OUTER,
  R_RIM,
  R_TICKS,
  RETICLE,
  VIEW_H,
  VIEW_W,
  nearestByAngle,
  placeRing,
  polar,
  toPercent,
  type PlacedNode,
} from "./orbital-geometry";
import styles from "./orbital-map.module.css";

export type OrbitalTone = "ok" | "warn" | "critical" | "neutral" | "accent" | "cyan" | "purple" | "gold";

export interface OrbitalNode {
  id: string;
  /** Short technical label shown next to the node (keep inner-ring labels to a few characters). */
  label: string;
  tone?: OrbitalTone;
  /** Native tooltip text. */
  title?: string;
}

export interface OrbitalReadoutLine {
  label: string;
  value: string;
}

interface OrbitalMapProps {
  /** Nodes on the inner ring, e.g. agents. Linked to the orb. */
  inner?: OrbitalNode[];
  /** Nodes on the outer ring, e.g. services. Each links to its nearest inner node. */
  outer?: OrbitalNode[];
  /** Callout for the first outer node (or the first inner node when there are no outer ones). */
  readout?: OrbitalReadoutLine[];
  /** Bottom-left caption. */
  caption?: string;
  /** Bottom-right caption. */
  meta?: string;
  /** Text alternative for the whole figure. */
  "aria-label": string;
  /** Drops the panel border and fill, for use inside another frame. */
  bare?: boolean;
  className?: string;
}

const MAX_INNER = 8;
const MAX_OUTER = 10;
/** Angle of the first outer node: upper left, so its callout runs to the left edge. */
const OUTER_START = -140;
const TICKS = Array.from({ length: 72 }, (_, i) => i * 5);
const RINGS = [R_RIM, R_INNER, R_OUTER];
/** Corner bracket of the square reticle; mirrored into the four corners. */
const BRACKET = 22;
/** Leaves the lattice clear of the rim ring even at its nearest, perspective-enlarged vertex. */
const LATTICE_RADIUS = R_ORB * 0.9;

/** Isometric (60°) grid tile: two verticals and one line of each 30° family. */
const TILE_W = 69.282;
const TILE_H = 40;

function reticlePath(cx: number, cy: number, half: number, arm: number): string {
  const corners: [number, number][] = [
    [-1, -1],
    [1, -1],
    [1, 1],
    [-1, 1],
  ];
  return corners
    .map(([sx, sy]) => {
      const x = cx + sx * half;
      const y = cy + sy * half;
      return `M ${x - sx * arm} ${y} H ${x} V ${y - sy * arm}`;
    })
    .join(" ");
}

function Label({ placed, className }: { placed: PlacedNode<OrbitalNode>; className: string }) {
  return (
    <span className={className} data-side={placed.side} style={toPercent(placed.labelPoint)}>
      {placed.node.label}
    </span>
  );
}

/**
 * Orbital node map: a lattice core framed by concentric wireframe rings, ticks and a square reticle over an
 * isometric grid, with data nodes on two rings and a callout readout for the focused node.
 */
export function OrbitalMap({
  inner = [],
  outer = [],
  readout,
  caption,
  meta,
  bare = false,
  className,
  "aria-label": ariaLabel,
}: OrbitalMapProps) {
  const gridId = `orbital-grid-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;

  const outerNodes = outer.slice(0, MAX_OUTER);
  const outerPlaced = placeRing(outerNodes, R_OUTER, OUTER_START, 16);
  // Inner nodes sit between outer ones so their labels and links don't collide.
  const innerStart = outerNodes.length > 0 ? OUTER_START + 180 / outerNodes.length : OUTER_START;
  const innerPlaced = placeRing(inner.slice(0, MAX_INNER), R_INNER, innerStart);
  const focus = outerPlaced[0] ?? innerPlaced[0];

  // Callout: diagonal out of the target box, then horizontal to the frame edge where the readout sits.
  const leader = focus
    ? (() => {
        const dir = focus.point.x < CX ? -1 : 1;
        const elbow = { x: focus.point.x + dir * 30, y: Math.max(focus.point.y - 30, 112) };
        const endX = dir < 0 ? 36 : VIEW_W - 36;
        return { dir, elbow, end: { x: endX, y: elbow.y } };
      })()
    : null;

  return (
    <figure
      className={[bare ? styles.bare : "frame", styles.root, className ?? ""].filter(Boolean).join(" ")}
      aria-label={ariaLabel}
    >
      <svg className={`${styles.layer} ${styles.grid}`} viewBox={`0 0 ${VIEW_W} ${VIEW_H}`} aria-hidden="true">
        <defs>
          <pattern id={gridId} width={TILE_W} height={TILE_H} patternUnits="userSpaceOnUse" x={CX} y={CY}>
            <path
              className={styles.gridLine}
              d={`M 0 0 V ${TILE_H} M ${TILE_W / 2} 0 V ${TILE_H} M 0 0 L ${TILE_W} ${TILE_H} M 0 ${TILE_H} L ${TILE_W} 0`}
            />
          </pattern>
        </defs>
        <rect width={VIEW_W} height={VIEW_H} fill={`url(#${gridId})`} />
      </svg>

      <div
        className={styles.glow}
        style={{
          left: `${((CX - R_ORB) / VIEW_W) * 100}%`,
          top: `${((CY - R_ORB) / VIEW_H) * 100}%`,
          width: `${((R_ORB * 2) / VIEW_W) * 100}%`,
        }}
      />

      <svg className={styles.layer} viewBox={`0 0 ${VIEW_W} ${VIEW_H}`} aria-hidden="true">
        <g className={styles.lines}>
          <line className={styles.faint} x1={CX - RETICLE} y1={CY} x2={CX + RETICLE} y2={CY} />
          <line className={styles.faint} x1={CX} y1={CY - RETICLE} x2={CX} y2={CY + RETICLE} />
          <rect className={styles.faint} x={CX - RETICLE} y={CY - RETICLE} width={RETICLE * 2} height={RETICLE * 2} />
          <path className={styles.strong} d={reticlePath(CX, CY, RETICLE, BRACKET)} />
          {RINGS.map((r) => (
            <circle key={r} cx={CX} cy={CY} r={r} className={r === R_INNER ? styles.dashed : undefined} />
          ))}
          <circle cx={CX} cy={CY} r={R_EDGE} className={`${styles.faint} ${styles.dashed}`} />
          <ellipse
            className={`${styles.faint} ${styles.dashed}`}
            cx={CX}
            cy={CY}
            rx={R_EDGE - 6}
            ry={84}
            transform={`rotate(-20 ${CX} ${CY})`}
          />
        </g>

        <g className={`${styles.lines} ${styles.spin}`}>
          {TICKS.map((deg) => {
            const major = deg % 30 === 0;
            const a = polar(R_TICKS, deg);
            const b = polar(R_TICKS + (major ? 12 : 6), deg);
            return (
              <line key={deg} className={major ? styles.strong : styles.faint} x1={a.x} y1={a.y} x2={b.x} y2={b.y} />
            );
          })}
        </g>

        <g className={styles.satellite}>
          <circle cx={CX + R_EDGE} cy={CY} r={3} />
        </g>

        <LatticeCore cx={CX} cy={CY} radius={LATTICE_RADIUS} />

        <g className={styles.links}>
          {innerPlaced.map((p) => {
            const rim = polar(R_RIM, p.deg);
            return <line key={p.node.id} x1={rim.x} y1={rim.y} x2={p.point.x} y2={p.point.y} />;
          })}
          {outerPlaced.map((p) => {
            const target = innerPlaced[nearestByAngle(innerPlaced, p.deg)]?.point ?? polar(R_RIM, p.deg);
            return <line key={p.node.id} x1={target.x} y1={target.y} x2={p.point.x} y2={p.point.y} />;
          })}
        </g>

        {[...innerPlaced, ...outerPlaced].map((p) => (
          <g key={p.node.id} className={styles.node} data-tone={p.node.tone ?? "neutral"}>
            {p.node.title && <title>{p.node.title}</title>}
            <circle className={styles.halo} cx={p.point.x} cy={p.point.y} r={8} />
            <circle className={styles.dot} cx={p.point.x} cy={p.point.y} r={3.5} />
          </g>
        ))}

        {focus && leader && (
          <g className={styles.callout}>
            <path d={reticlePath(focus.point.x, focus.point.y, 14, 5)} />
            <polyline
              points={`${focus.point.x + leader.dir * 14},${focus.point.y - 14} ${leader.elbow.x},${leader.elbow.y} ${leader.end.x},${leader.end.y}`}
            />
          </g>
        )}
      </svg>

      <div className={styles.overlay} aria-hidden="true">
        {/* The focused node is named by the readout instead of a label. */}
        {innerPlaced.map(
          (p) => p !== focus && <Label key={p.node.id} placed={p} className={`${styles.label} ${styles.innerLabel}`} />,
        )}
        {outerPlaced.map((p) => p !== focus && <Label key={p.node.id} placed={p} className={styles.label} />)}

        {focus && leader && readout && readout.length > 0 && (
          <dl className={styles.readout} data-side={leader.dir < 0 ? "left" : "right"} style={toPercent(leader.end)}>
            {readout.map((line) => (
              <div key={line.label} className={styles.readoutRow}>
                <dt>{line.label}</dt>
                <dd>{line.value}</dd>
              </div>
            ))}
          </dl>
        )}

        {caption && <span className={`${styles.caption} ${styles.captionLeft}`}>{caption}</span>}
        {meta && <span className={`${styles.caption} ${styles.captionRight}`}>{meta}</span>}
      </div>
    </figure>
  );
}
