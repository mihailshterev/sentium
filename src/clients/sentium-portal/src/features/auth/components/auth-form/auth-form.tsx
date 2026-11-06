import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, ArrowRight, Lock, User } from "lucide-react";
import styles from "./auth-form.module.css";
import { BASE_URL } from "@/shared/api/config";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/form-controls";
import { registerSchema } from "../../schemas/auth.register";
import { loginSchema, type LoginFormData } from "../../schemas/auth.login";

type Mode = "login" | "register";

const MODES: { id: Mode; label: string }[] = [
  { id: "login", label: "Sign in" },
  { id: "register", label: "Register" },
];

function FieldError({ children }: { children: React.ReactNode }) {
  return (
    <p className={styles.error} role="alert">
      <AlertCircle size={14} aria-hidden="true" />
      {children}
    </p>
  );
}

export const AuthForm = () => {
  const [mode, setMode] = useState<Mode>("login");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<LoginFormData>({
    resolver: zodResolver(mode === "login" ? loginSchema : registerSchema),
    defaultValues: { userName: "", password: "" },
  });

  const switchMode = (next: Mode) => {
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
        const regRes = await fetch(`${BASE_URL}/identity/account/register`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          credentials: "include",
          body: JSON.stringify({ userName: data.userName, password: data.password }),
        });

        if (!regRes.ok) {
          setError("Registration failed. The username may already be in use.");
          setSubmitting(false);
          return;
        }
      }

      const loginRes = await fetch(`${BASE_URL}/identity/account/login?returnUrl=${encodeURIComponent(returnUrl)}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ userName: data.userName, password: data.password }),
      });

      if (!loginRes.ok) {
        if (loginRes.status === 423) {
          setError("Account is locked due to too many failed attempts.");
        } else {
          setError(
            mode === "login"
              ? "Invalid username or password."
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

  const submitLabel =
    mode === "login"
      ? submitting
        ? "Signing in..."
        : "Sign in"
      : submitting
        ? "Creating account..."
        : "Create account";

  return (
    <div className={styles.root}>
      {/* Plain buttons (not tabs): e2e page objects switch modes by button name. */}
      <div className={styles.segment} role="group" aria-label="Account">
        {MODES.map((m) => (
          <button
            key={m.id}
            type="button"
            className={styles.segmentButton}
            aria-pressed={mode === m.id}
            onClick={() => switchMode(m.id)}
          >
            {m.label}
          </button>
        ))}
      </div>

      <form key={mode} className={styles.form} onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={styles.field}>
          <label className={styles.label} htmlFor="userName">
            Username
          </label>
          <span className={styles.inputWrap}>
            <User size={15} className={styles.inputIcon} aria-hidden="true" />
            <Input
              id="userName"
              className={styles.input}
              type="text"
              placeholder="Your username"
              autoComplete="username"
              invalid={!!errors.userName}
              {...register("userName")}
            />
          </span>
          {errors.userName && <FieldError>{errors.userName.message}</FieldError>}
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="password">
            Password
          </label>
          <span className={styles.inputWrap}>
            <Lock size={15} className={styles.inputIcon} aria-hidden="true" />
            <Input
              id="password"
              className={styles.input}
              type="password"
              placeholder={mode === "login" ? "Your password" : "Create a password"}
              autoComplete={mode === "login" ? "current-password" : "new-password"}
              invalid={!!errors.password}
              {...register("password")}
            />
          </span>
          {errors.password && <FieldError>{errors.password.message}</FieldError>}
        </div>

        {error && <FieldError>{error}</FieldError>}

        <Button
          type="submit"
          variant="primary"
          size="lg"
          fullWidth
          loading={submitting}
          iconRight={<ArrowRight size={16} aria-hidden="true" />}
          className={styles.submit}
        >
          {submitLabel}
        </Button>
      </form>
    </div>
  );
};
