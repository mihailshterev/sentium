import { useState, type FormEvent } from "react";
import { Navigate } from "react-router";
import { AlertCircle, ArrowRight, Bot, Lock, Mail, ShieldCheck, Zap } from "lucide-react";
import styles from "./login.module.scss";
import { AnimatedBg } from "./animated-bg";
import { useAuthStore } from "../../stores/auth-store";
import { API_BASE, BFF_BASE } from "../../utils/constants";

const FEATURES = [
  "Real-time threat detection and autonomous response",
  "AI-powered agent orchestration across your network",
  "Zero-trust identity enforcement at every layer",
  "Continuous behavioral analysis and anomaly detection",
];

const Login = () => {
  const status = useAuthStore((state) => state.status);
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (status === "authenticated") {
    return <Navigate to="/" replace />;
  }

  const switchMode = (next: "login" | "register") => {
    setMode(next);
    setError(null);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const endpoint = mode === "login" ? `${API_BASE}/identity/account/login` : `${API_BASE}/identity/account/register`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ email, password }),
      });

      if (!res.ok) {
        setError(
          mode === "login" ? "Invalid email or password." : "Registration failed. The email may already be in use.",
        );
        setSubmitting(false);
        return;
      }

      window.location.href = `${BFF_BASE}/login?returnUrl=${encodeURIComponent(window.location.origin + "/")}`;
    } catch {
      setError("Something went wrong. Please try again.");
      setSubmitting(false);
    }
  };

  return (
    <div className={styles.root}>
      <div className={styles.leftPanel}>
        <AnimatedBg className={styles.bgCanvas} />
        <div className={styles.scanLine} />

        <div className={styles.leftTop}>
          <div className={styles.leftBrandIcon}>
            <ShieldCheck size={18} strokeWidth={2} />
          </div>
          <span className={styles.leftBrandName}>Sentium</span>
        </div>

        <div className={styles.leftCenter}>
          <h1 className={styles.headline}>
            Intelligent Security,
            <br />
            <span>Autonomously</span>
            <br />
            Enforced.
          </h1>
          <p className={styles.descriptor}>
            Sentium unifies AI-driven threat detection, autonomous agent orchestration, and zero-trust identity
            enforcement into a single cohesive platform.
          </p>
          <ul className={styles.featureList}>
            {FEATURES.map((f) => (
              <li key={f} className={styles.featureItem}>
                <span className={styles.featureDot} />
                {f}
              </li>
            ))}
          </ul>
        </div>

        <div className={styles.leftBottom}>
          <div className={styles.statsRow}>
            <div className={styles.stat}>
              <span className={styles.statValue}>99.9%</span>
              <span className={styles.statLabel}>Uptime</span>
            </div>
            <div className={styles.stat}>
              <span className={styles.statValue}>&lt;50ms</span>
              <span className={styles.statLabel}>Response</span>
            </div>
            <div className={styles.stat}>
              <span className={styles.statValue}>24/7</span>
              <span className={styles.statLabel}>Monitoring</span>
            </div>
          </div>
          <div className={styles.statusRow}>
            <span className={styles.statusDot} />
            All systems operational
          </div>
        </div>
      </div>

      <div className={styles.rightPanel}>
        <div className={styles.card}>
          <div className={styles.cardHeader}>
            <div className={styles.logoMark}>
              <Bot size={24} strokeWidth={1.75} />
            </div>
            <h2 className={styles.cardTitle}>Sentium</h2>
            <p className={styles.cardSubtitle}>
              {mode === "login" ? "Sign in to your account to continue" : "Create an account to get started"}
            </p>
          </div>

          <div className={styles.tabs}>
            <button
              type="button"
              className={`${styles.tab} ${mode === "login" ? styles.tabActive : ""}`}
              onClick={() => switchMode("login")}
            >
              Sign in
            </button>
            <button
              type="button"
              className={`${styles.tab} ${mode === "register" ? styles.tabActive : ""}`}
              onClick={() => switchMode("register")}
            >
              Register
            </button>
          </div>

          <form className={styles.form} onSubmit={handleSubmit}>
            <div className={styles.field}>
              <label className={styles.label} htmlFor="email">
                Email address
              </label>
              <div className={styles.inputWrapper}>
                <span className={styles.inputIcon}>
                  <Mail size={15} strokeWidth={1.75} />
                </span>
                <input
                  id="email"
                  className={styles.input}
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  required
                  autoComplete="email"
                />
              </div>
            </div>

            <div className={styles.field}>
              <label className={styles.label} htmlFor="password">
                Password
              </label>
              <div className={styles.inputWrapper}>
                <span className={styles.inputIcon}>
                  <Lock size={15} strokeWidth={1.75} />
                </span>
                <input
                  id="password"
                  className={styles.input}
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder={mode === "login" ? "Your password" : "Create a password"}
                  required
                  autoComplete={mode === "login" ? "current-password" : "new-password"}
                />
              </div>
            </div>

            {error && (
              <div className={styles.error}>
                <AlertCircle size={14} strokeWidth={2} className={styles.errorIcon} />
                {error}
              </div>
            )}

            <button type="submit" className={styles.submitButton} disabled={submitting}>
              {submitting ? (
                <>
                  <span className={styles.spinner} />
                  {mode === "login" ? "Signing in..." : "Creating account..."}
                </>
              ) : (
                <>
                  {mode === "login" ? "Sign in" : "Create account"}
                  <ArrowRight size={15} strokeWidth={2} />
                </>
              )}
            </button>
          </form>

          <div className={styles.divider} />

          <p className={styles.footerNote}>
            {mode === "login" ? (
              <>
                Don't have an account?{" "}
                <a href="#" onClick={() => switchMode("register")}>
                  Create one
                </a>
              </>
            ) : (
              <>
                Already have an account?{" "}
                <a href="#" onClick={() => switchMode("login")}>
                  Sign in
                </a>
              </>
            )}
          </p>

          <p className={styles.footerNote}>
            <Zap size={11} style={{ display: "inline", verticalAlign: "middle", marginRight: 4 }} />
            Secured by Sentium Identity &amp; zero-trust enforcement
          </p>
        </div>
      </div>
    </div>
  );
};

export default Login;
