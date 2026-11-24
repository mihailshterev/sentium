import { useForm } from "react-hook-form";
import { AlertCircle, CheckCircle2, KeyRound, LogOut } from "lucide-react";
import { zodResolver } from "@/shared/forms/zod-resolver";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { Input } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { useAccountSecurity } from "../hooks/useAccountSecurity";
import { changePasswordSchema, type ChangePasswordFormData } from "../schemas/change-password";
import styles from "../profile.module.css";

/** Change password and sign out of other browsers. */
export function SecurityCard({ className }: { className?: string }) {
  const { changePassword, signOutOthers } = useAccountSecurity();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ChangePasswordFormData>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });

  const onSubmit = async (data: ChangePasswordFormData) => {
    signOutOthers.reset();
    try {
      await changePassword.mutateAsync({ currentPassword: data.currentPassword, newPassword: data.newPassword });
      reset();
    } catch {
      // surfaced through changePassword.error
    }
  };

  const onSignOutOthers = () => {
    changePassword.reset();
    signOutOthers.mutate();
  };

  return (
    <Card padding="none" as="section" className={className} data-testid="account-security">
      <CardHeader
        icon={<KeyRound />}
        title="Sign-in & security"
        description="Changing your password signs you out of every other browser within a minute."
      />

      <div className={styles.formWrap}>
        <form onSubmit={handleSubmit(onSubmit)} className={styles.form} noValidate>
          <FormField id="currentPassword" label="Current password" error={errors.currentPassword?.message}>
            <Input
              id="currentPassword"
              type="password"
              autoComplete="current-password"
              invalid={!!errors.currentPassword}
              {...register("currentPassword")}
            />
          </FormField>
          <div className={styles.row}>
            <FormField id="newPassword" label="New password" error={errors.newPassword?.message}>
              <Input
                id="newPassword"
                type="password"
                autoComplete="new-password"
                invalid={!!errors.newPassword}
                {...register("newPassword")}
              />
            </FormField>
            <FormField id="confirmPassword" label="Repeat new password" error={errors.confirmPassword?.message}>
              <Input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                invalid={!!errors.confirmPassword}
                {...register("confirmPassword")}
              />
            </FormField>
          </div>

          {changePassword.error && (
            <StatusMessage variant="error" icon={<AlertCircle />} message={changePassword.error.message} />
          )}
          {changePassword.isSuccess && (
            <StatusMessage variant="success" icon={<CheckCircle2 />} message="Password changed." />
          )}
          {signOutOthers.error && (
            <StatusMessage variant="error" icon={<AlertCircle />} message={signOutOthers.error.message} />
          )}
          {signOutOthers.isSuccess && (
            <StatusMessage
              variant="success"
              icon={<CheckCircle2 />}
              message="Your other browsers will be signed out within a minute."
            />
          )}

          <div className={styles.securityActions}>
            <Button
              type="button"
              variant="ghost"
              icon={<LogOut size={15} />}
              loading={signOutOthers.isPending}
              onClick={onSignOutOthers}
            >
              Sign out other browsers
            </Button>
            <Button type="submit" variant="primary" icon={<KeyRound size={15} />} loading={changePassword.isPending}>
              Change password
            </Button>
          </div>
        </form>
      </div>
    </Card>
  );
}
