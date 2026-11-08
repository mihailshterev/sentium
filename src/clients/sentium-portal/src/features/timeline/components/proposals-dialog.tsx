import { Sparkles } from "lucide-react";
import styles from "../timeline.module.css";
import { Dialog } from "@/shared/ui/dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import type { TimelineProposal } from "../types/timeline";
import { ProposalCard, type ProposalActions } from "./proposal-card";

interface ProposalsDialogProps extends ProposalActions {
  proposals: TimelineProposal[];
  onClose: () => void;
}

export function ProposalsDialog({ proposals, onClose, ...actions }: ProposalsDialogProps) {
  return (
    <Dialog
      open
      onClose={onClose}
      title="Agent suggestions"
      description="Tasks and moves your agents proposed. Nothing changes until you accept."
      icon={<Sparkles />}
      size="md"
    >
      {proposals.length === 0 ? (
        <EmptyState compact icon={<Sparkles />} title="No suggestions waiting" hint="Ask an agent to plan a month." />
      ) : (
        <ul className={styles.proposals}>
          {proposals.map((proposal) => (
            <ProposalCard key={proposal.id} proposal={proposal} {...actions} />
          ))}
        </ul>
      )}
    </Dialog>
  );
}
