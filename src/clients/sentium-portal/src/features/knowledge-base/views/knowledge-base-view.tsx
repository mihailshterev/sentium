import { useState } from "react";
import { BrainCircuit, FlaskConical, UserRoundSearch } from "lucide-react";
import { Page } from "@/shared/ui/page";
import { Tabs } from "@/shared/ui/tabs";
import { AgentLearningsTab, GlobalContextTab } from "../components/knowledge-base-tabs";
import { MemoriesTab } from "../components/memories-tab";

type Tab = "context" | "learnings" | "memories";

const TABS = [
  { id: "context", label: "Global Context", icon: <FlaskConical />, testId: "tab-global-context" },
  { id: "learnings", label: "Agent Learnings", icon: <BrainCircuit />, testId: "tab-agent-learnings" },
  { id: "memories", label: "My Memories", icon: <UserRoundSearch />, testId: "tab-my-memories" },
];

export function KnowledgeBaseView() {
  const [activeTab, setActiveTab] = useState<Tab>("context");

  return (
    <Page title="Knowledge Base" subtitle="Global agent context, vector store statistics, captured learnings and what agents remember about you">
      <Tabs tabs={TABS} active={activeTab} onChange={(id) => setActiveTab(id as Tab)} label="Knowledge base views" />
      {activeTab === "context" && <GlobalContextTab />}
      {activeTab === "learnings" && <AgentLearningsTab />}
      {activeTab === "memories" && <MemoriesTab />}
    </Page>
  );
}
