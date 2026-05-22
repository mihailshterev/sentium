import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, CheckCircle2, Mail, Save, UserRound } from "lucide-react";
import styles from "../profile.module.scss";
import type { UserProfile } from "../../../services/identity.service";
import StatusMessage from "../../../components/ui/status-message";
import { userProfileSchema, type UserProfileFormData } from "../../../schemas/user.profile";

type UpdateProfilePayload = { firstName: string; lastName?: string | null; email: string };

interface ProfileEditFormProps {
  profile: UserProfile;
  updateProfile: (payload: UpdateProfilePayload) => Promise<void>;
  isSaving: boolean;
  saveError: Error | null;
  isSaveSuccess: boolean;
  resetSave: () => void;
}

const ProfileEditForm = ({
  profile,
  updateProfile,
  isSaving,
  saveError,
  isSaveSuccess,
  resetSave,
}: ProfileEditFormProps) => {
  const { register, handleSubmit } = useForm<UserProfileFormData>({
    resolver: zodResolver(userProfileSchema),
    defaultValues: {
      firstName: profile.firstName,
      lastName: profile.lastName ?? "",
      email: profile.email,
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
        email: data.email.trim(),
      });
    } catch {
      // error surfaced via saveError
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className={styles.form}>
      <div className={styles.formRow}>
        <div className={styles.field}>
          <label htmlFor="firstName" className={styles.label}>
            <UserRound size={12} />
            First Name
          </label>
          <input
            id="firstName"
            type="text"
            className={styles.input}
            placeholder="First name"
            {...register("firstName")}
          />
        </div>
        <div className={styles.field}>
          <label htmlFor="lastName" className={styles.label}>
            <UserRound size={12} />
            Last Name
          </label>
          <input
            id="lastName"
            type="text"
            className={styles.input}
            placeholder="Last name (optional)"
            {...register("lastName")}
          />
        </div>
      </div>

      <div className={styles.field}>
        <label htmlFor="email" className={styles.label}>
          <Mail size={12} />
          Email Address
        </label>
        <input
          id="email"
          type="email"
          className={styles.input}
          placeholder="email@example.com"
          {...register("email")}
        />
      </div>

      {saveError && <StatusMessage variant="error" icon={<AlertCircle size={13} />} message={saveError.message} />}

      {isSaveSuccess && (
        <StatusMessage variant="success" icon={<CheckCircle2 size={13} />} message="Profile updated successfully." />
      )}

      <div className={styles.formActions}>
        <button type="submit" className={styles.saveBtn} disabled={isSaving}>
          <Save size={13} />
          {isSaving ? "Saving…" : "Save Changes"}
        </button>
      </div>
    </form>
  );
};

export default ProfileEditForm;
