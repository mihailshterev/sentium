import { Badge } from "@/shared/ui/badge";

export function AlignmentBadge({ verdict }: { verdict: string | null }) {
  if (!verdict) {
    return <span aria-label="No alignment check">—</span>;
  }
  const tone = verdict === "Aligned" ? "success" : verdict === "Misaligned" ? "danger" : "neutral";
  return <Badge tone={tone}>{verdict}</Badge>;
}
