import { ArrowRight, Check, Sparkles, X } from "lucide-react";
import styles from "../timeline.module.css";
import type { TimelineProposal } from "../types/timeline";
import { formatShortDay, monthLabel } from "../utils/calendar";
import { describeProposal } from "../utils/display";

export interface ProposalActions {
  onAccept: (proposal: TimelineProposal) => void;
  onReject: (proposal: TimelineProposal) => void;
  busyId?: string | null;
}

const where = (year: number, month: number, date: string | null) =>
  date ? formatShortDay(date) : `${monthLabel(year, month)} (pinned)`;

export function ProposalGhost({
  proposal,
  onAccept,
  onReject,
  busyId,
}: { proposal: TimelineProposal } & ProposalActions) {
  const busy = busyId === proposal.id;
  return (
    <div className={styles.ghost} data-kind={proposal.kind.toLowerCase()} title={proposal.rationale ?? undefined}>
      <Sparkles size={10} aria-hidden="true" />
      <span className={styles.chipTitle}>{proposal.title}</span>
      <button
        type="button"
        className={styles.ghostAction}
        aria-label={`Accept suggestion: ${describeProposal(proposal)}`}
        disabled={busy}
        onClick={() => onAccept(proposal)}
      >
        <Check size={11} />
      </button>
      <button
        type="button"
        className={styles.ghostAction}
        aria-label={`Reject suggestion: ${describeProposal(proposal)}`}
        disabled={busy}
        onClick={() => onReject(proposal)}
      >
        <X size={11} />
      </button>
    </div>
  );
}

export function ProposalCard({
  proposal,
  onAccept,
  onReject,
  busyId,
}: { proposal: TimelineProposal } & ProposalActions) {
  const busy = busyId === proposal.id;
  const isMove = proposal.kind === "Reschedule";

  return (
    <li className={styles.proposal} data-testid={`proposal-${proposal.id}`}>
      <div className={styles.proposalHead}>
        <Sparkles size={13} aria-hidden="true" />
        <span className={styles.proposalTitle}>{describeProposal(proposal)}</span>
      </div>
      <p className={styles.proposalWhere}>
        {isMove && proposal.fromYear && proposal.fromMonth && (
          <>
            {where(proposal.fromYear, proposal.fromMonth, proposal.fromDate)}
            <ArrowRight size={11} aria-hidden="true" />
          </>
        )}
        {where(proposal.year, proposal.month, proposal.date)}
      </p>
      {proposal.rationale && <p className={styles.proposalWhy}>{proposal.rationale}</p>}
      <div className={styles.proposalFoot}>
        <span className={styles.proposalBy}>by {proposal.proposedBy}</span>
        <button
          type="button"
          className={styles.proposalReject}
          disabled={busy}
          aria-label={`Reject suggestion: ${describeProposal(proposal)}`}
          onClick={() => onReject(proposal)}
        >
          Reject
        </button>
        <button
          type="button"
          className={styles.proposalAccept}
          disabled={busy}
          aria-label={`Accept suggestion: ${describeProposal(proposal)}`}
          onClick={() => onAccept(proposal)}
        >
          Accept
        </button>
      </div>
    </li>
  );
}
