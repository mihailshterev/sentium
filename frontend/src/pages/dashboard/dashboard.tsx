import { useNavigate } from "react-router";
import { ArrowRight } from "lucide-react";
import styles from "./dashboard.module.scss";

const Dashboard = () => {
  const navigate = useNavigate();

  return (
    <div className={styles.homeContainer}>
      <div className={styles.gridOverlay}></div>
      <div className={styles.glowOrb}></div>

      <div className={styles.heroContent}>
        <div className={styles.badge}>
          <span className="status-dot"></span>
          System Ready
        </div>

        <h1 className={styles.heroTitle}>
          <span className={styles.titleLine}>Intelligent</span>
          <span className={styles.titleLineAccent}>Agent Orchestration</span>
        </h1>

        <p className={styles.heroSubtitle}>
          Advanced threat modeling, automated forensics, and AI-driven response
          protocols. Connect and deploy your agent pipeline instantly.
        </p>

        <div className={styles.ctaRow}>
          <button
            className={styles.initBtn}
            onClick={() => navigate("/orchestration")}
          >
            <span>Launch Orchestration</span>
            <ArrowRight size={16} />
          </button>
          <button
            className={styles.secondaryBtn}
            onClick={() => navigate("/agents")}
          >
            Manage Agents
          </button>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
