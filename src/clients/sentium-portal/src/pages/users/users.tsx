import { useState } from "react";
import { UsersRound, RefreshCw, AlertCircle, UserX } from "lucide-react";
import styles from "./users.module.scss";
import { ROLE_HIERARCHY, type Role } from "../../utils/roles";
import { useRole } from "../../hooks/useRole";
import { useAuthStore } from "../../stores/auth-store";
import useUsers from "../../hooks/useUsers";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import SkeletonRows from "./components/skeleton-rows";
import UserRow from "./components/user-row";

const ROLE_OPTIONS = ROLE_HIERARCHY as readonly Role[];

export default function Users() {
  const [actionError, setActionError] = useState<string | null>(null);
  const [pendingUserId, setPendingUserId] = useState<string | null>(null);

  const { isSovereign } = useRole();
  const currentUser = useAuthStore((s) => s.user);
  const { users, isLoading, isFetching, error, refetch, assignRole, removeRole, deleteUser } = useUsers();

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

  const handleRemoveRole = async (userId: string, role: string) => {
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

  const handleDeleteUser = async (userId: string) => {
    if (!window.confirm("Are you sure you want to permanently delete this user?")) {
      return;
    }
    setActionError(null);
    setPendingUserId(userId);
    try {
      await deleteUser(userId);
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : "Failed to delete user.");
    } finally {
      setPendingUserId(null);
    }
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
            <span className={styles.userCount}>
              {!isFetching && `${users.length} user${users.length !== 1 ? "s" : ""}`}
            </span>
          </div>

          <div className={styles.tableHeader}>
            <span className={styles.colName}>Name</span>
            <span className={styles.colEmail}>Email</span>
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
        </div>
      </div>
    </div>
  );
}
