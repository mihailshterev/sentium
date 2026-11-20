import { AlertCircle } from "lucide-react";
import styles from "../profile.module.css";
import { useRole } from "@/shared/auth/hooks/useRole";
import { MyDevicesCard } from "@/features/devices";
import { useAvatar } from "@/features/profile/hooks/useAvatar";
import { useProfile } from "@/features/profile/hooks/useProfile";
import { Badge } from "@/shared/ui/badge";
import { Card } from "@/shared/ui/card";
import { Page } from "@/shared/ui/page";
import { Skeleton, SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { AvatarPicker } from "../components/avatar-picker";
import { ProfileEditForm } from "../components/profile-edit-form";

export function ProfileView() {
  const { highestRole } = useRole();
  const { profile, isLoading, updateProfile, isSaving, saveError, isSaveSuccess, resetSave } = useProfile();
  const avatar = useAvatar();

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
              <AvatarPicker
                name={displayName}
                avatarUrl={profile?.avatarUrl ?? null}
                isUploading={avatar.isUploading}
                isRemoving={avatar.isRemoving}
                onPick={avatar.uploadAvatar}
                onRemove={avatar.removeAvatar}
              />
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

            {avatar.error && (
              <StatusMessage
                variant="error"
                icon={<AlertCircle />}
                message={avatar.error.message}
                className={styles.avatarError}
              />
            )}

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

      <MyDevicesCard className={styles.card} />
    </Page>
  );
}
