import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, ArrowRight, Lock, Mail, Zap } from "lucide-react";
import styles from "./auth-form.module.scss";
import { registerSchema } from "../../schemas/auth.register";
import { loginSchema, type LoginFormData } from "../../schemas/auth.login";

export const AuthForm = () => {
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

  const switchMode = (next: "login" | "register") => {
    setMode(next);
    setError(null);
    reset();
  };

  const getReturnUrl = () => {
    const params = new URLSearchParams(window.location.search);
    return params.get("returnUrl") ?? "/";
  };

  const onSubmit = async (data: LoginFormData) => {
    setError(null);
    setSubmitting(true);

    const returnUrl = getReturnUrl();

    try {
      if (mode === "register") {
        const regRes = await fetch("/account/register", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          credentials: "include",
          body: JSON.stringify({ email: data.email, password: data.password }),
        });

        if (!regRes.ok) {
          setError("Registration failed. The email may already be in use.");
          setSubmitting(false);
          return;
        }
      }

      const loginRes = await fetch(`/account/login?returnUrl=${encodeURIComponent(returnUrl)}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ email: data.email, password: data.password }),
      });

      if (!loginRes.ok) {
        if (loginRes.status === 423) {
          setError("Account is locked due to too many failed attempts.");
        } else {
          setError(
            mode === "login"
              ? "Invalid email or password."
              : "Account created but sign-in failed. Please try logging in.",
          );
        }
        setSubmitting(false);
        return;
      }

      const result = await loginRes.json();
      window.location.assign(result.redirectUrl ?? returnUrl);
    } catch {
      setError("Something went wrong. Please try again.");
      setSubmitting(false);
    }
  };

  return (
    <div className={styles.card}>
      <p className={styles.cardSubtitle}>
        {mode === "login" ? "Sign in to your account to continue" : "Create an account to get started"}
      </p>

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
  );
};
