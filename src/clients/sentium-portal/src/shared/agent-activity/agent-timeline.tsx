import { useEffect, useId, useState, type ReactNode } from "react";
import Markdown from "react-markdown";
import { ChevronRight } from "lucide-react";
import { Button } from "@/shared/ui/button";
import styles from "./agent-timeline.module.css";
import { formatDuration } from "./format";
import { parseJson } from "./reduce-activity";
import type { ActivityStep, OutputStep, StatusStep, TextStep, ThoughtStep, ToolStatus, ToolStep } from "./types";

export interface TimelineApproval {
  callId?: string | null;
  toolName: string;
  disabled?: boolean;
  onDecide: (approved: boolean) => void;
}

interface AgentTimelineProps {
  steps: ActivityStep[];
  live?: boolean;
  collapsible?: boolean;
  hasAnswer?: boolean;
  approval?: TimelineApproval;
  renderAuthor?: (author: string) => ReactNode;
  renderOutput?: (step: OutputStep) => ReactNode;
  className?: string;
}

const STATUS_LABEL: Partial<Record<ToolStatus, string>> = {
  awaiting_approval: "awaiting approval",
  error: "failed",
  denied: "blocked by policy",
  rejected: "declined",
};

function useNow(active: boolean, intervalMs = 200) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!active) return;
    const timer = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(timer);
  }, [active, intervalMs]);
  return now;
}

const summarizeArgs = (args: string | undefined) => {
  if (!args) return "";
  const parsed = parseJson<unknown>(args);
  if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
    const values = Object.values(parsed as Record<string, unknown>);
    const first = values.find((v) => v !== null && v !== undefined && v !== "");
    if (first === undefined) return "";
    const text = typeof first === "string" ? first : JSON.stringify(first);
    return values.length > 1 ? `${text} …` : text;
  }
  return args;
};

const firstLine = (text: string) => text.trim().split(/\n+/)[0] ?? "";

const dotTone = (step: ActivityStep, live: boolean): string => {
  switch (step.kind) {
    case "thought":
      return "thought";
    case "text":
    case "output":
      return "text";
    case "status":
      return step.tone;
    case "tool":
      if (step.status === "running") return live ? "running" : "idle";
      return step.status;
  }
};

function StepShell({ step, live, children }: { step: ActivityStep; live: boolean; children: ReactNode }) {
  return (
    <li className={styles.step} data-kind={step.kind} data-tone={dotTone(step, live)}>
      <span className={styles.dot} aria-hidden="true" />
      <div className={styles.body}>{children}</div>
    </li>
  );
}

function ThoughtRow({ step, active }: { step: ThoughtStep; active: boolean }) {
  const [open, setOpen] = useState(false);
  const bodyId = useId();
  const elapsed = step.startedAt !== undefined && step.endedAt !== undefined ? step.endedAt - step.startedAt : 0;
  const duration = elapsed >= 1000 ? formatDuration(elapsed) : null;
  const label = active ? "thinking" : duration ? `thought · ${duration}` : "thought";

  return (
    <>
      <button
        type="button"
        className={styles.head}
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-controls={bodyId}
      >
        <span className={`${styles.label} ${active ? styles.labelActive : ""}`}>{label}</span>
        {!open && !active && <span className={styles.preview}>{firstLine(step.text)}</span>}
        <ChevronRight size={13} className={`${styles.chevron} ${open ? styles.chevronOpen : ""}`} aria-hidden="true" />
      </button>
      {open ? (
        <div id={bodyId} className={`${styles.prose} ${styles.thoughtText}`}>
          <Markdown>{step.text}</Markdown>
        </div>
      ) : (
        active && (
          <div id={bodyId} className={styles.tail}>
            <span>{step.text.slice(-420)}</span>
          </div>
        )
      )}
    </>
  );
}

function ArgumentsView({ args }: { args: string }) {
  const parsed = parseJson<unknown>(args);
  if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
    const entries = Object.entries(parsed as Record<string, unknown>);
    if (entries.length === 0) return null;
    return (
      <dl className={styles.args}>
        {entries.map(([key, value]) => (
          <div key={key} className={styles.argRow}>
            <dt>{key}</dt>
            <dd>{typeof value === "string" ? value : JSON.stringify(value)}</dd>
          </div>
        ))}
      </dl>
    );
  }
  return <pre className={styles.code}>{args}</pre>;
}

function ToolRow({ step, live, approval }: { step: ToolStep; live: boolean; approval?: TimelineApproval }) {
  const [open, setOpen] = useState(false);
  const bodyId = useId();
  const ticking = live && step.status === "running" && step.startedAt !== undefined;
  const now = useNow(ticking);
  const awaiting = step.status === "awaiting_approval" && approval !== undefined;

  const time =
    step.durationMs !== undefined
      ? formatDuration(step.durationMs)
      : ticking
        ? formatDuration(now - (step.startedAt as number))
        : null;
  const statusLabel = STATUS_LABEL[step.status];
  const summary = summarizeArgs(step.arguments);
  const hasDetails = Boolean(step.arguments || step.result);

  const headContent = (
    <>
      <span className={styles.toolName}>{step.name}</span>
      {summary && <span className={styles.preview}>{summary}</span>}
      <span className={styles.meta}>
        {statusLabel && <span className={styles.statusText}>{statusLabel}</span>}
        {time && <span className={styles.time}>{time}</span>}
      </span>
    </>
  );

  return (
    <>
      {hasDetails ? (
        <button
          type="button"
          className={styles.head}
          onClick={() => setOpen((v) => !v)}
          aria-expanded={open}
          aria-controls={bodyId}
        >
          {headContent}
          <ChevronRight
            size={13}
            className={`${styles.chevron} ${open ? styles.chevronOpen : ""}`}
            aria-hidden="true"
          />
        </button>
      ) : (
        <div className={styles.head} data-static>
          {headContent}
        </div>
      )}

      {open && (
        <div id={bodyId} className={styles.details}>
          {step.arguments && (
            <section>
              <h4 className={styles.detailLabel}>input</h4>
              <ArgumentsView args={step.arguments} />
            </section>
          )}
          {step.result && (
            <section>
              <h4 className={styles.detailLabel}>
                {step.status === "error" ? "error" : step.status === "denied" ? "policy" : "result"}
              </h4>
              <pre className={styles.code}>{step.result}</pre>
            </section>
          )}
        </div>
      )}

      {awaiting && (
        <div className={styles.approval} role="group" aria-label="Tool approval">
          <p className={styles.approvalText}>
            <span className={styles.approvalTitle}>Approval required.</span> The agent wants to run{" "}
            <code>{step.name}</code>
            {open || !step.arguments ? "." : " with:"}
          </p>
          {!open && step.arguments && <ArgumentsView args={step.arguments} />}
          <div className={styles.approvalActions}>
            <Button variant="secondary" size="sm" onClick={() => approval.onDecide(false)} disabled={approval.disabled}>
              Deny
            </Button>
            <Button variant="primary" size="sm" onClick={() => approval.onDecide(true)} disabled={approval.disabled}>
              Approve
            </Button>
          </div>
        </div>
      )}
    </>
  );
}

function TextRow({ step }: { step: TextStep }) {
  return (
    <div className={`${styles.prose} ${styles.narration}`}>
      <Markdown>{step.text}</Markdown>
    </div>
  );
}

function StatusRow({ step }: { step: StatusStep }) {
  return <p className={styles.status}>{step.text}</p>;
}

const pluralize = (n: number, word: string) => `${n} ${word}${n === 1 ? "" : "s"}`;

function Summary({
  steps,
  open,
  onToggle,
  controls,
}: {
  steps: ActivityStep[];
  open: boolean;
  onToggle: () => void;
  controls: string;
}) {
  const tools = steps.filter((s): s is ToolStep => s.kind === "tool");
  const thoughts = steps.filter((s): s is ThoughtStep => s.kind === "thought");
  const failed = tools.filter((t) => t.status === "error" || t.status === "denied").length;
  const thinkMs = thoughts.reduce(
    (sum, t) => sum + (t.startedAt !== undefined && t.endedAt !== undefined ? t.endedAt - t.startedAt : 0),
    0,
  );

  const parts: string[] = [];
  if (thoughts.length > 0)
    parts.push(thinkMs >= 1000 ? `thought for ${formatDuration(thinkMs)}` : pluralize(thoughts.length, "thought"));
  if (tools.length > 0) parts.push(pluralize(tools.length, "tool call"));
  if (failed > 0) parts.push(`${failed} failed`);
  if (parts.length === 0) parts.push(pluralize(steps.length, "step"));

  return (
    <button type="button" className={styles.summary} onClick={onToggle} aria-expanded={open} aria-controls={controls}>
      <span className={styles.strip} aria-hidden="true">
        {steps.slice(0, 12).map((s) => (
          <span key={s.id} className={styles.stripDot} data-tone={dotTone(s, false)} />
        ))}
      </span>
      <span className={styles.summaryText}>{parts.join(" · ")}</span>
      <ChevronRight size={13} className={`${styles.chevron} ${open ? styles.chevronOpen : ""}`} aria-hidden="true" />
    </button>
  );
}

/**
 * An agent's activity as a vertical timeline: each thought, narration, tool call (with input, result and timing) and
 * milestone is its own dot on a hairline rail, in the order it happened.
 */
export function AgentTimeline({
  steps,
  live = false,
  collapsible = false,
  hasAnswer = true,
  approval,
  renderAuthor,
  renderOutput,
  className,
}: AgentTimelineProps) {
  const listId = useId();
  const [userOpen, setUserOpen] = useState<boolean | null>(null);
  const awaiting = approval !== undefined && steps.some((s) => s.kind === "tool" && s.status === "awaiting_approval");
  const open = !collapsible || (userOpen ?? (live || awaiting || !hasAnswer));

  const [wasLive, setWasLive] = useState(live);
  if (wasLive !== live) {
    setWasLive(live);
    if (live) setUserOpen(null);
  }

  if (steps.length === 0) return null;

  const lastIndex = steps.length - 1;
  const showAuthorAt = steps.map((step, i) => {
    if (!renderAuthor || !step.author || step.kind === "status") return false;
    return steps.slice(0, i).findLast((s) => s.kind !== "status")?.author !== step.author;
  });

  return (
    <div className={`${styles.root} ${className ?? ""}`}>
      {collapsible && !live && (
        <Summary steps={steps} open={open} onToggle={() => setUserOpen(!open)} controls={listId} />
      )}
      {open && (
        <ol id={listId} className={styles.timeline} data-live={live || undefined}>
          {steps.map((step, i) => {
            const showAuthor = showAuthorAt[i];

            const active = live && i === lastIndex && step.kind === "thought" && step.endedAt === undefined;
            const matchesApproval =
              approval &&
              step.kind === "tool" &&
              step.status === "awaiting_approval" &&
              (approval.callId ? step.callId === approval.callId : step.name === approval.toolName);

            return (
              <StepShell key={step.id} step={step} live={live}>
                {showAuthor && <div className={styles.author}>{renderAuthor?.(step.author as string)}</div>}
                {step.kind === "thought" && <ThoughtRow step={step} active={active} />}
                {step.kind === "tool" && (
                  <ToolRow step={step} live={live} approval={matchesApproval ? approval : undefined} />
                )}
                {step.kind === "text" && <TextRow step={step} />}
                {step.kind === "status" && <StatusRow step={step} />}
                {step.kind === "output" &&
                  (renderOutput?.(step) ?? (
                    <div className={styles.prose}>
                      <Markdown>{step.text}</Markdown>
                    </div>
                  ))}
              </StepShell>
            );
          })}
        </ol>
      )}
    </div>
  );
}
