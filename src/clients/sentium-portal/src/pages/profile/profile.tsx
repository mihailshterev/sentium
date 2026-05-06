import { useState, useEffect } from "react";
import { Save, AlertCircle, CheckCircle2, Mail, UserRound, ShieldUser } from "lucide-react";
import styles from "./profile.module.scss";
import type { UserProfile } from "../../services/identity.service";
import { useRole } from "../../hooks/useRole";
import { AnimatedBg } from "../login/animated-bg";
import useProfile from "../../hooks/useProfile";

type UpdateProfilePayload = { firstName: string; lastName?: string | null; email: string };

interface ProfileEditFormProps {
  profile: UserProfile;
  updateProfile: (payload: UpdateProfilePayload) => Promise<void>;
  isSaving: boolean;
  saveError: Error | null;
  isSaveSuccess: boolean;
  resetSave: () => void;
}

function ProfileEditForm({
  profile,
  updateProfile,
  isSaving,
  saveError,
  isSaveSuccess,
  resetSave,
}: ProfileEditFormProps) {
  const [firstName, setFirstName] = useState(profile.firstName);
  const [lastName, setLastName] = useState(profile.lastName ?? "");
  const [email, setEmail] = useState(profile.email);

  useEffect(() => {
    if (isSaveSuccess) {
      const timer = setTimeout(resetSave, 3000);
      return () => clearTimeout(timer);
    }
  }, [isSaveSuccess, resetSave]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await updateProfile({
        firstName: firstName.trim(),
        lastName: lastName.trim() || null,
        email: email.trim(),
      });
    } catch {
      // error surfaced via saveError
    }
  };

  return (
    <form onSubmit={handleSave} className={styles.form}>
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
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            required
            maxLength={100}
            placeholder="First name"
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
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            maxLength={100}
            placeholder="Last name (optional)"
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
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          maxLength={256}
          placeholder="email@example.com"
        />
      </div>

      {saveError && (
        <div className={styles.errorBanner}>
          <AlertCircle size={13} />
          <span>{saveError.message}</span>
        </div>
      )}

      {isSaveSuccess && (
        <div className={styles.successBanner}>
          <CheckCircle2 size={13} />
          <span>Profile updated successfully.</span>
        </div>
      )}

      <div className={styles.formActions}>
        <button type="submit" className={styles.saveBtn} disabled={isSaving}>
          <Save size={13} />
          {isSaving ? "Saving…" : "Save Changes"}
        </button>
      </div>
    </form>
  );
}

export default function Profile() {
  const { highestRole } = useRole();
  const { profile, isLoading, updateProfile, isSaving, saveError, isSaveSuccess, resetSave } = useProfile();

  return (
    <div className={styles.root}>
      <AnimatedBg className={styles.bgCanvas} />
      <div className={styles.scanLine} />

      <div className={styles.centreWrap}>
        {isLoading ? (
          <div className={styles.skeleton} />
        ) : (
          <div className={styles.card}>
            <div className={styles.avatarSection}>
              <div className={styles.avatarCircle}>
                <ShieldUser size={54} strokeWidth={1.5} />
              </div>
              <div className={styles.avatarMeta}>
                <p className={styles.displayName}>
                  {profile ? `${profile.firstName || ""} ${profile.lastName || ""}`.trim() || profile.email : "—"}
                </p>
                <p className={styles.displayEmail}>{profile?.email}</p>
                {highestRole && (
                  <span className={`${styles.roleBadge} ${styles[`role_${highestRole.toLowerCase()}`]}`}>
                    {highestRole}
                  </span>
                )}
              </div>
            </div>

            <div className={styles.divider} />

            {profile && (
              <ProfileEditForm
                key={profile.id}
                profile={profile}
                updateProfile={updateProfile}
                isSaving={isSaving}
                saveError={saveError}
                isSaveSuccess={isSaveSuccess}
                resetSave={resetSave}
              />
            )}
          </div>
        )}
      </div>
    </div>
  );
}
