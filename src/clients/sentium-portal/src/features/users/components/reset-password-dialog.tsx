import { useId } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { KeyRound } from "lucide-react";
import { newPasswordSchema } from "@/features/auth/schemas/auth.password";
import { zodResolver } from "@/shared/forms/zod-resolver";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";

const resetPasswordSchema = z.object({ newPassword: newPasswordSchema });
type ResetPasswordFormData = z.infer<typeof resetPasswordSchema>;

interface ResetPasswordDialogProps {
  /** Who is being reset, as shown to the Sovereign; null closes the dialog. */
  target: { id: string; identifier: string } | null;
  isSaving: boolean;
  error: Error | null;
  onSubmit: (userId: string, newPassword: string) => void | Promise<void>;
  onClose: () => void;
}

/** Sovereign sets a new password for a household member who forgot theirs. */
export function ResetPasswordDialog({ target, isSaving, error, onSubmit, onClose }: ResetPasswordDialogProps) {
  const formId = useId();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ResetPasswordFormData>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: "" },
  });

  const close = () => {
    reset();
    onClose();
  };

  return (
    <Dialog
      open={target !== null}
      onClose={close}
      title="Reset password"
      description={`Set a new password for "${target?.identifier ?? ""}". The account is unlocked and signed out everywhere; tell them the new password.`}
      icon={<KeyRound />}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={close}>
            Cancel
          </Button>
          <Button type="submit" form={formId} variant="primary" loading={isSaving}>
            Reset password
          </Button>
        </>
      }
    >
      <form
        id={formId}
        noValidate
        onSubmit={handleSubmit((data) => {
          if (target) {
            void onSubmit(target.id, data.newPassword);
          }
        })}
      >
        <FormField id="resetNewPassword" label="New password" error={errors.newPassword?.message}>
          <Input
            id="resetNewPassword"
            type="password"
            autoComplete="new-password"
            invalid={!!errors.newPassword}
            {...register("newPassword")}
          />
        </FormField>
        {error && <StatusMessage variant="error" message={error.message} />}
      </form>
    </Dialog>
  );
}
