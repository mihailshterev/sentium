import { Cpu } from "lucide-react";
import styles from "./landing.module.scss";
import { FlowField } from "../../components/flow-field";
import { AuthForm } from "../../components/auth-form/auth-form";

const Landing = () => {
  return (
    <div className={styles.root}>
      <FlowField className={styles.field} />
      <div className={styles.vignette} />

      <div className={styles.content}>
        <div className={styles.brand}>
          <div className={styles.brandIcon}>
            <Cpu size={20} strokeWidth={2} />
          </div>
          <span className={styles.brandName}>Sentium</span>
        </div>

        <p className={styles.tagline}>
          Local AI workflows, <span>autonomously orchestrated.</span>
        </p>

        <div className={styles.cardWrap}>
          <AuthForm />
        </div>
      </div>
    </div>
  );
};

export default Landing;
