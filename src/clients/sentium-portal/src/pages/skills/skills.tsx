import { useState } from "react";
import { Bot, Cpu, Upload, Zap } from "lucide-react";
import styles from "./skills.module.scss";
import PageHeader from "../../components/ui/page-header";
import BuiltInTab from "./components/built-in-tab";
import CustomTab from "./components/custom-tab";
import UploadedTab from "./components/uploaded-tab";

type Tab = "built-in" | "custom" | "uploaded";

const Skills = () => {
  const [activeTab, setActiveTab] = useState<Tab>("built-in");

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<Zap size={18} className={styles.titleIcon} />}
        title="Agent Skills"
        subtitle="Manage built-in and custom skills that extend what agents can do"
      />

      <div className={styles.tabs}>
        <button
          className={`${styles.tab} ${activeTab === "built-in" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("built-in")}
          data-testid="tab-builtin"
        >
          <Cpu size={14} />
          Built-in
        </button>
        <button
          className={`${styles.tab} ${activeTab === "custom" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("custom")}
          data-testid="tab-custom"
        >
          <Bot size={14} />
          Custom
        </button>
        <button
          className={`${styles.tab} ${activeTab === "uploaded" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("uploaded")}
          data-testid="tab-uploaded"
        >
          <Upload size={14} />
          Uploaded
        </button>
      </div>

      <div className={styles.body}>
        {activeTab === "built-in" && <BuiltInTab />}
        {activeTab === "custom" && <CustomTab />}
        {activeTab === "uploaded" && <UploadedTab />}
      </div>
    </div>
  );
};

export default Skills;
