import { useState } from "react";
import { Bot, Cpu, Upload } from "lucide-react";
import { Page } from "@/shared/ui/page";
import { Tabs } from "@/shared/ui/tabs";
import { BuiltInTab } from "../components/built-in-tab";
import { CustomTab } from "../components/custom-tab";
import { UploadedTab } from "../components/uploaded-tab";

type Tab = "built-in" | "custom" | "uploaded";

const TABS = [
  { id: "built-in", label: "Built-in", icon: <Cpu />, testId: "tab-builtin" },
  { id: "custom", label: "Custom", icon: <Bot />, testId: "tab-custom" },
  { id: "uploaded", label: "Uploaded", icon: <Upload />, testId: "tab-uploaded" },
];

export function SkillsView() {
  const [activeTab, setActiveTab] = useState<Tab>("built-in");

  return (
    <Page title="Agent Skills" subtitle="Manage built-in and custom skills that extend what agents can do">
      <Tabs tabs={TABS} active={activeTab} onChange={(id) => setActiveTab(id as Tab)} label="Skill sources" />

      {activeTab === "built-in" && <BuiltInTab />}
      {activeTab === "custom" && <CustomTab />}
      {activeTab === "uploaded" && <UploadedTab />}
    </Page>
  );
}
