import { OrbitalMap, toSlug, type OrbitalNode, type OrbitalReadoutLine, type OrbitalTone } from "@/shared/ui/orbital";
import type { AgentRecord } from "@/features/agents/types/agents";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";

const SERVICE_TONE: Record<string, OrbitalTone> = {
  Healthy: "ok",
  Degraded: "warn",
  Unhealthy: "critical",
};

interface SystemMapProps {
  services: ServiceHealthStatus[];
  agents: AgentRecord[];
  className?: string;
}

/** Live topology: services on the outer ring (coloured by health), agents on the inner ring. */
export function SystemMap({ services, agents, className }: SystemMapProps) {
  const healthy = services.filter((s) => s.status === "Healthy").length;

  const outer: OrbitalNode[] = services.map((s) => ({
    id: `svc-${s.serviceName}`,
    label: toSlug(s.serviceName),
    tone: SERVICE_TONE[s.status] ?? "neutral",
    title: `${s.serviceName} — ${s.status.toLowerCase()}, ${s.latencyMs} ms`,
  }));

  // Agents get catalogue codes (a01, a02…); full names are in the tooltips.
  const inner: OrbitalNode[] = agents.map((a, i) => ({
    id: `agt-${a.id}`,
    label: `a${String(i + 1).padStart(2, "0")}`,
    tone: "cyan",
    title: `${a.name} — ${a.model}`,
  }));

  const [firstService] = services;
  const [firstAgent] = agents;
  let readout: OrbitalReadoutLine[] | undefined;
  if (firstService) {
    readout = [
      { label: "node", value: toSlug(firstService.serviceName) },
      { label: "state", value: firstService.status.toLowerCase() },
      { label: "lat", value: `${firstService.latencyMs} ms` },
      { label: "up", value: `${firstService.uptimePercent.toFixed(1)}%` },
    ];
  } else if (firstAgent) {
    readout = [
      { label: "node", value: toSlug(firstAgent.name) },
      { label: "model", value: firstAgent.model },
    ];
  }

  return (
    <OrbitalMap
      className={className}
      outer={outer}
      inner={inner}
      readout={readout}
      caption="fig.01 — system topology"
      meta={`${services.length} svc · ${agents.length} agt`}
      aria-label={`System map: ${healthy} of ${services.length} services healthy, ${agents.length} agents`}
    />
  );
}
