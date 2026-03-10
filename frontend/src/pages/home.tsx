import { useNavigate } from "react-router";
import styles from "./home.module.scss";

export default function Home() {
  const navigate = useNavigate();

  return (
    <div className={styles["home-container"]}>
      <div className={styles["grid-overlay"]}></div>

      <div className={styles["hero-content"]}>
        <div className={styles["badge"]}>SYSTEM_READY</div>
        <h1 className={styles["hero-title"]}>
          SENTIUM <span className={styles["highlight"]}>ORCHESTRATOR</span>
        </h1>
        <p className={styles["hero-subtitle"]}>
          Advanced threat modeling, automated forensics, and AI-driven response
          protocols. Establish secure connection to begin session.
        </p>

        <button
          className={styles["init-btn"]}
          onClick={() => navigate("/terminal")}
        >
          <span>INITIALIZE TERMINAL</span>
          <span className={styles["btn-cursor"]}>_</span>
        </button>
      </div>
    </div>
  );
}
