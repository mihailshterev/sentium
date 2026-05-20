import { useState } from "react";
import { BrainCircuit, Database, FlaskConical } from "lucide-react";
import styles from "./knowledge-base.module.scss";
import PageHeader from "../../components/ui/page-header";
import { GlobalContextTab, AgentLearningsTab } from "./components/knowledge-base-tabs";

type Tab = "context" | "learnings";

const KnowledgeBase = () => {
  const [activeTab, setActiveTab] = useState<Tab>("context");

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<Database size={18} className={styles.titleIcon} />}
        title="Knowledge Base"
        subtitle="Global agent context, vector store statistics, and captured learnings"
      />

      <div className={styles.tabs}>
        <button
          className={`${styles.tab} ${activeTab === "context" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("context")}
        >
          <FlaskConical size={14} />
          Global Context
        </button>
        <button
          className={`${styles.tab} ${activeTab === "learnings" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("learnings")}
        >
          <BrainCircuit size={14} />
          Agent Learnings
        </button>
      </div>

      <div className={styles.body}>
        {activeTab === "context" && <GlobalContextTab />}
        {activeTab === "learnings" && <AgentLearningsTab />}
      </div>
    </div>
  );
};

export default KnowledgeBase;
