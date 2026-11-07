import { useState } from "react";
import { BrainCircuit, FlaskConical } from "lucide-react";
import { Page } from "@/shared/ui/page";
import { Tabs } from "@/shared/ui/tabs";
import { AgentLearningsTab, GlobalContextTab } from "../components/knowledge-base-tabs";

type Tab = "context" | "learnings";

const TABS = [
  { id: "context", label: "Global Context", icon: <FlaskConical />, testId: "tab-global-context" },
  { id: "learnings", label: "Agent Learnings", icon: <BrainCircuit />, testId: "tab-agent-learnings" },
];

export function KnowledgeBaseView() {
  const [activeTab, setActiveTab] = useState<Tab>("context");

  return (
    <Page title="Knowledge Base" subtitle="Global agent context, vector store statistics, and captured learnings">
      <Tabs tabs={TABS} active={activeTab} onChange={(id) => setActiveTab(id as Tab)} label="Knowledge base views" />
      {activeTab === "context" && <GlobalContextTab />}
      {activeTab === "learnings" && <AgentLearningsTab />}
    </Page>
  );
}
