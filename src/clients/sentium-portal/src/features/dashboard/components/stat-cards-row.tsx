import { Bot, BrainCircuit, Clock, GitBranch, Library, ShieldCheck } from "lucide-react";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";

interface StatCardsRowProps {
  agentsCount: number;
  workflowsCount: number;
  modelsCount: number;
  auditTotal: number;
  kbCollections: number;
  cronJobs: number;
  loading: boolean;
}

export function StatCardsRow({
  agentsCount,
  workflowsCount,
  modelsCount,
  auditTotal,
  kbCollections,
  cronJobs,
  loading,
}: StatCardsRowProps) {
  const value = (n: number) => (loading ? "—" : n.toLocaleString());

  return (
    <StatGrid columns={3} aria-busy={loading || undefined}>
      <StatCard icon={<Bot />} value={value(agentsCount)} label="Agents" />
      <StatCard icon={<GitBranch />} value={value(workflowsCount)} label="Workflows" />
      <StatCard icon={<BrainCircuit />} value={value(modelsCount)} label="AI Models" />
      <StatCard icon={<ShieldCheck />} value={value(auditTotal)} label="Audit Events" />
      <StatCard icon={<Library />} value={value(kbCollections)} label="KB Collections" />
      <StatCard icon={<Clock />} value={value(cronJobs)} label="Cron Jobs" />
    </StatGrid>
  );
}
