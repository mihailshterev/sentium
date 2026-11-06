import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, CheckCircle2, Save } from "lucide-react";
import styles from "../profile.module.css";
import type { UserProfile } from "../../auth/api/identity.service";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { userProfileSchema, type UserProfileFormData } from "../schemas/user.profile";

type UpdateProfilePayload = { firstName: string; lastName?: string | null; userName: string };

interface ProfileEditFormProps {
  profile: UserProfile;
  updateProfile: (payload: UpdateProfilePayload) => Promise<void>;
  isSaving: boolean;
  saveError: Error | null;
  isSaveSuccess: boolean;
  resetSave: () => void;
}

export function ProfileEditForm({
  profile,
  updateProfile,
  isSaving,
  saveError,
  isSaveSuccess,
  resetSave,
}: ProfileEditFormProps) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<UserProfileFormData>({
    resolver: zodResolver(userProfileSchema),
    defaultValues: {
      firstName: profile.firstName,
      lastName: profile.lastName ?? "",
      userName: profile.userName,
    },
  });

  useEffect(() => {
    if (isSaveSuccess) {
      const timer = setTimeout(resetSave, 3000);
      return () => clearTimeout(timer);
    }
  }, [isSaveSuccess, resetSave]);

  const onSubmit = async (data: UserProfileFormData) => {
    try {
      await updateProfile({
        firstName: data.firstName.trim(),
        lastName: data.lastName?.trim() || null,
        userName: data.userName.trim(),
      });
    } catch {
      // error surfaced via saveError
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className={styles.form}>
      <div className={styles.row}>
        <FormField id="firstName" label="First Name" error={errors.firstName?.message}>
          <Input
            id="firstName"
            type="text"
            placeholder="First name"
            invalid={!!errors.firstName}
            {...register("firstName")}
          />
        </FormField>
        <FormField id="lastName" label="Last Name">
          <Input id="lastName" type="text" placeholder="Last name (optional)" {...register("lastName")} />
        </FormField>
      </div>

      <FormField id="userName" label="Username" error={errors.userName?.message}>
        <Input
          id="userName"
          type="text"
          placeholder="Username"
          autoComplete="username"
          invalid={!!errors.userName}
          {...register("userName")}
        />
      </FormField>

      {saveError && <StatusMessage variant="error" icon={<AlertCircle />} message={saveError.message} />}
      {isSaveSuccess && (
        <StatusMessage variant="success" icon={<CheckCircle2 />} message="Profile updated successfully." />
      )}

      <div className={styles.actions}>
        <Button type="submit" variant="primary" icon={<Save size={15} />} loading={isSaving}>
          {isSaving ? "Saving…" : "Save Changes"}
        </Button>
      </div>
    </form>
  );
}
