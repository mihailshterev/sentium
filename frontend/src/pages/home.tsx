import { useNavigate } from "react-router";
import styles from "./home.module.scss";

const Home = () => {
  const navigate = useNavigate();

  return (
    <div className={styles.homeContainer}>
      <div className={styles.gridOverlay}></div>

      <div className={styles.heroContent}>
        <div className={styles.badge}>SYSTEM_READY</div>
        <h1 className={styles.heroTitle}>
          SENTIUM <span className={styles.highlight}>ORCHESTRATOR</span>
        </h1>
        <p className={styles.heroSubtitle}>
          Advanced threat modeling, automated forensics, and AI-driven response
          protocols. Establish secure connection to begin session.
        </p>

        <button
          className={styles.initBtn}
          onClick={() => navigate("/terminal")}
        >
          <span>INITIALIZE TERMINAL</span>
          <span className={styles.btnCursor}>_</span>
        </button>
      </div>
    </div>
  );
};

export default Home;
