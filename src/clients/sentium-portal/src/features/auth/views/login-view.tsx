import styles from "./landing.module.css";
import { AuthForm } from "../components/auth-form/auth-form";
import { CosmosBackground } from "@/shared/ui/cosmos-background";
import { OrbitalMap, type OrbitalNode } from "@/shared/ui/orbital";
import { SystemReadout } from "@/shared/ui/page-header";

/** The platform's modules, drawn around the orb as a quiet map of what sits behind the sign-in. */
const MODULES: OrbitalNode[] = [
  "identity",
  "registry",
  "sentinel",
  "codex",
  "locus",
  "tempus",
  "praxis",
  "vigil",
  "sandbox",
].map((name) => ({ id: name, label: name, tone: name === "identity" ? "accent" : "neutral" }));

export function LoginView() {
  return (
    <div className={styles.root}>
      <CosmosBackground />

      <main className={`frame ${styles.panel}`}>
        <section className={styles.left}>
          <header className={styles.topBar}>
            <span className={styles.brand}>
              <img src="/sentium-mark.png" alt="" width={28} height={28} />
              Sentium
            </span>
          </header>

          <div className={styles.copy}>
            <span className={styles.eyebrow}>secure access · local runtime</span>
            <h1 className={styles.headline}>
              Local AI workflows, <span className={styles.headlineMuted}>autonomously orchestrated.</span>
            </h1>
            <p className={styles.lede}>
              Agents, tools and knowledge on your own hardware. Every action is checked by policy before it runs.
            </p>
          </div>

          <div className={styles.form}>
            <AuthForm />
          </div>

          <footer className={styles.footer}>
            <span>policy-sandboxed</span>
            <span>no data leaves the host</span>
          </footer>
        </section>

        <section className={styles.right} aria-hidden="true">
          <span className={styles.readout}>
            <SystemReadout />
          </span>
          <OrbitalMap
            bare
            className={styles.map}
            outer={MODULES}
            readout={[
              { label: "node", value: "identity" },
              { label: "auth", value: "cookie" },
              { label: "state", value: "awaiting" },
            ]}
            caption="fig.00 — platform"
            meta={`${MODULES.length} modules`}
            aria-label="Platform modules"
          />
        </section>
      </main>
    </div>
  );
}
