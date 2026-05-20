import { ShieldUser } from "lucide-react";
import styles from "./profile.module.scss";
import { useRole } from "../../hooks/useRole";
import { AnimatedBg } from "../login/animated-bg";
import useProfile from "../../hooks/useProfile";
import ProfileEditForm from "./components/profile-edit-form";

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
