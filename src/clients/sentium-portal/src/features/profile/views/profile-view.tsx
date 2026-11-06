import styles from "../profile.module.css";
import { useRole } from "@/shared/auth/hooks/useRole";
import { useProfile } from "@/features/profile/hooks/useProfile";
import { Badge } from "@/shared/ui/badge";
import { Card } from "@/shared/ui/card";
import { Page } from "@/shared/ui/page";
import { Skeleton, SkeletonText } from "@/shared/ui/skeleton";
import { ProfileEditForm } from "../components/profile-edit-form";

function initialsOf(first?: string | null, last?: string | null, userName?: string): string {
  const initials = `${first?.[0] ?? ""}${last?.[0] ?? ""}`.toUpperCase();
  return initials || (userName?.slice(0, 2).toUpperCase() ?? "?");
}

export function ProfileView() {
  const { highestRole } = useRole();
  const { profile, isLoading, updateProfile, isSaving, saveError, isSaveSuccess, resetSave } = useProfile();

  const displayName = profile ? `${profile.firstName || ""} ${profile.lastName || ""}`.trim() || profile.userName : "—";

  return (
    <Page title="Profile" subtitle="Your account details">
      <Card padding="none" className={styles.card}>
        {isLoading ? (
          <div className={styles.loading} aria-busy="true">
            <Skeleton width={72} height={72} radius="full" />
            <SkeletonText lines={3} />
          </div>
        ) : (
          <>
            <div className={styles.identity}>
              <span className={styles.avatar} aria-hidden="true">
                {initialsOf(profile?.firstName, profile?.lastName, profile?.userName)}
              </span>
              <div className={styles.identityText}>
                <p className={styles.displayName}>{displayName}</p>
                <p className={styles.username}>{profile?.userName}</p>
              </div>
              {highestRole && (
                <Badge tone={highestRole === "Sovereign" ? "accent" : "info"} className={styles.role}>
                  {highestRole}
                </Badge>
              )}
            </div>

            {profile && (
              <div className={styles.formWrap}>
                <ProfileEditForm
                  key={profile.id}
                  profile={profile}
                  updateProfile={updateProfile}
                  isSaving={isSaving}
                  saveError={saveError}
                  isSaveSuccess={isSaveSuccess}
                  resetSave={resetSave}
                />
              </div>
            )}
          </>
        )}
      </Card>
    </Page>
  );
}
