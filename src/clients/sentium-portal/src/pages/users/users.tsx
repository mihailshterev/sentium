import { useState } from "react";
import { UsersRound, RefreshCw, AlertCircle, UserX, ChevronLeft, ChevronRight } from "lucide-react";
import styles from "./users.module.scss";
import { ROLE_HIERARCHY, type Role } from "../../utils/roles";
import { useRole } from "../../hooks/useRole";
import { useAuthStore } from "../../stores/auth-store";
import useUsers from "../../hooks/useUsers";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import SkeletonRows from "./components/skeleton-rows";
import UserRow from "./components/user-row";
import ConfirmDialog from "../../components/ui/confirm-dialog"; // Added import

const ROLE_OPTIONS = ROLE_HIERARCHY as readonly Role[];

export default function Users() {
  const [actionError, setActionError] = useState<string | null>(null);
  const [pendingUserId, setPendingUserId] = useState<string | null>(null);

  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const [userToDelete, setUserToDelete] = useState<{ id: string; identifier: string } | null>(null);

  const { isSovereign } = useRole();
  const currentUser = useAuthStore((s) => s.user);
  const {
    users,
    totalCount,
    totalPages,
    page,
    setPage,
    isLoading,
    isFetching,
    error,
    refetch,
    assignRole,
    removeRole,
    deleteUser,
  } = useUsers();

  const handleAssignRole = async (userId: string, role: Role) => {
    setActionError(null);
    setPendingUserId(userId);
    try {
      await assignRole({ userId, roleName: role });
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : "Failed to assign role.");
    } finally {
      setPendingUserId(null);
    }
  };

  const handleRemoveRole = async (userId: string, role: Role) => {
    setActionError(null);
    setPendingUserId(userId);
    try {
      await removeRole({ userId, roleName: role });
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : "Failed to remove role.");
    } finally {
      setPendingUserId(null);
    }
  };

  const handleDeleteUser = (userId: string) => {
    const targetUser = users.find((u) => u.id === userId);
    const fullName = targetUser ? `${targetUser.firstName} ${targetUser.lastName || ""}`.trim() : "";

    const identifier = fullName || targetUser?.email || "this user";

    setUserToDelete({ id: userId, identifier });
    setIsConfirmOpen(true);
  };

  const handleConfirmDelete = async () => {
    if (!userToDelete) {
      return;
    }

    setActionError(null);
    setPendingUserId(userToDelete.id);
    const targetId = userToDelete.id;

    setIsConfirmOpen(false);
    setUserToDelete(null);

    try {
      await deleteUser(targetId);
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : "Failed to delete user.");
    } finally {
      setPendingUserId(null);
    }
  };

  const handleCancelDelete = () => {
    setIsConfirmOpen(false);
    setUserToDelete(null);
  };

  const isSelf = (userId: string) => currentUser?.sub === userId;

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<UsersRound size={18} />}
        title="User Management"
        subtitle="Manage system users and role assignments"
        right={
          <div className={styles.headerRight}>
            {actionError && (
              <div className={styles.errorInline}>
                <AlertCircle size={12} />
                <span>{actionError}</span>
                <button onClick={() => setActionError(null)} className={styles.errorDismiss}>
                  ✕
                </button>
              </div>
            )}
            <button
              className={`${styles.refreshBtn} ${isFetching ? styles.spinning : ""}`}
              onClick={() => refetch()}
              disabled={isFetching}
            >
              <RefreshCw size={12} />
              Refresh
            </button>
          </div>
        }
      />

      <div className={styles.body}>
        <div className={styles.section}>
          <div className={styles.sectionHeader}>
            <div className={styles.sectionTitle}>
              <UsersRound size={13} className={styles.sectionTitleIcon} />
              Registered Users
            </div>
            <span className={styles.userCount}>{!isLoading && `${totalCount} user${totalCount !== 1 ? "s" : ""}`}</span>
          </div>

          <div className={styles.tableHeader}>
            <span className={styles.colAvatar} />
            <span className={styles.colUser}>User</span>
            <span className={styles.colRoles}>Roles</span>
            {isSovereign && <span className={styles.colAssign}>Assign / Remove</span>}
            {isSovereign && <span className={styles.colActions} />}
          </div>

          <div className={styles.tableBody}>
            {isLoading ? (
              <SkeletonRows />
            ) : error ? (
              <EmptyState icon={<AlertCircle size={28} />} title="Failed to load users" hint={error.message} />
            ) : users.length === 0 ? (
              <EmptyState icon={<UserX size={28} />} title="No users found" />
            ) : (
              users.map((user) => (
                <UserRow
                  key={user.id}
                  user={user}
                  isSelf={isSelf(user.id)}
                  isSovereign={isSovereign}
                  isPending={pendingUserId === user.id}
                  roleOptions={ROLE_OPTIONS}
                  onAssignRole={handleAssignRole}
                  onRemoveRole={handleRemoveRole}
                  onDeleteUser={handleDeleteUser}
                />
              ))
            )}
          </div>

          {totalPages > 1 && (
            <div className={styles.pagination}>
              <button
                className={styles.pageBtn}
                onClick={() => setPage((p) => p - 1)}
                disabled={page <= 1 || isFetching}
              >
                <ChevronLeft size={13} />
              </button>
              <span className={styles.pageInfo}>
                {page} / {totalPages}
              </span>
              <button
                className={styles.pageBtn}
                onClick={() => setPage((p) => p + 1)}
                disabled={page >= totalPages || isFetching}
              >
                <ChevronRight size={13} />
              </button>
            </div>
          )}
        </div>
      </div>

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Permanently Delete User"
        description={`Are you sure you want to permanently delete "${userToDelete?.identifier || ""}"? This user will immediately lose access and all related configurations will be lost.`}
        confirmLabel="Delete User"
        cancelLabel="Cancel"
        onConfirm={handleConfirmDelete}
        onCancel={handleCancelDelete}
        confirmWord={userToDelete?.identifier ?? undefined}
      />
    </div>
  );
}
