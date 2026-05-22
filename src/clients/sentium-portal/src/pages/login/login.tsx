import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Navigate } from "react-router";
import { AlertCircle, ArrowRight, Bot, Cpu, Lock, Mail, Zap } from "lucide-react";
import styles from "./login.module.scss";
import { AnimatedBg } from "./animated-bg";
import { useAuthStore } from "../../stores/auth-store";
import { BASE_URL, BFF_BASE } from "../../api/client";
import { AUTH_STATUS } from "../../utils/constants";
import { loginSchema, type LoginFormData } from "../../schemas/auth.login";
import { registerSchema } from "../../schemas/auth.register";

const FEATURES = [
  "Local LLM execution with complete data privacy",
  "Autonomous multi-agent orchestration & execution",
  "Zero-trust policy sandboxing & security guardrails",
];

const Login = () => {
  const status = useAuthStore((state) => state.status);
  const [mode, setMode] = useState<"login" | "register">("login");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<LoginFormData>({
    resolver: zodResolver(mode === "login" ? loginSchema : registerSchema),
  });

  if (status === AUTH_STATUS.AUTHENTICATED) {
    return <Navigate to="/" replace />;
  }

  const switchMode = (next: "login" | "register") => {
    setMode(next);
    setError(null);
    reset();
  };

  const onSubmit = async (data: LoginFormData) => {
    setError(null);
    setSubmitting(true);

    const endpoint = mode === "login" ? `${BASE_URL}/identity/account/login` : `${BASE_URL}/identity/account/register`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ email: data.email, password: data.password }),
      });

      if (!res.ok) {
        setError(
          mode === "login" ? "Invalid email or password." : "Registration failed. The email may already be in use.",
        );
        setSubmitting(false);
        return;
      }

      const params = new URLSearchParams(window.location.search);
      const redirectTarget = params.get("returnUrl") || "/";

      window.location.href = `${BFF_BASE}/login?returnUrl=${encodeURIComponent(window.location.origin + redirectTarget)}`;
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
            <Cpu size={18} strokeWidth={2} />
          </div>
          <span className={styles.leftBrandName}>Sentium</span>
        </div>

        <div className={styles.leftCenter}>
          <h1 className={styles.headline}>
            Local AI Workflows,
            <br />
            <span>Autonomously</span>
            <br />
            Orchestrated.
          </h1>
          <p className={styles.descriptor}>
            Sentium unifies local private AI execution, autonomous multi-agent orchestration, and secure zero-trust
            policy sandboxing into a premium cohesive platform.
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
              <span className={styles.statValue}>100%</span>
              <span className={styles.statLabel}>Local &amp; Private</span>
            </div>
            <div className={styles.stat}>
              <span className={styles.statValue}>&lt;10ms</span>
              <span className={styles.statLabel}>Agent Latency</span>
            </div>
            <div className={styles.stat}>
              <span className={styles.statValue}>Zero</span>
              <span className={styles.statLabel}>Cloud Dependency</span>
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

          <form className={styles.form} onSubmit={handleSubmit(onSubmit)}>
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
                  placeholder="you@example.com"
                  autoComplete="email"
                  {...register("email")}
                />
              </div>
              {errors.email && (
                <div className={styles.error}>
                  <AlertCircle size={14} strokeWidth={2} className={styles.errorIcon} />
                  {errors.email.message}
                </div>
              )}
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
                  placeholder={mode === "login" ? "Your password" : "Create a password"}
                  autoComplete={mode === "login" ? "current-password" : "new-password"}
                  {...register("password")}
                />
              </div>
              {errors.password && (
                <div className={styles.error}>
                  <AlertCircle size={14} strokeWidth={2} className={styles.errorIcon} />
                  {errors.password.message}
                </div>
              )}
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
            Powered by Sentium local AI runtime &amp; policy sandboxing
          </p>
        </div>
      </div>
    </div>
  );
};

export default Login;
