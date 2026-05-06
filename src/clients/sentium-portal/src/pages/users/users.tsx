import { useState } from "react";
import { UsersRound, Trash2, ShieldMinus, RefreshCw, AlertCircle, UserX } from "lucide-react";
import styles from "./users.module.scss";
import { ROLE_HIERARCHY, type Role } from "../../utils/roles";
import { useRole } from "../../hooks/useRole";
import { useAuthStore } from "../../stores/auth-store";
import useUsers from "../../hooks/useUsers";

const ROLE_OPTIONS = ROLE_HIERARCHY as readonly Role[];

function RoleBadge({ role }: { role: string }) {
  return <span className={`${styles.roleBadge} ${styles[`role_${role.toLowerCase()}`]}`}>{role}</span>;
}

function SkeletonRows() {
  return (
    <>
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className={styles.skeletonRow}>
          <div className={styles.skeletonCell} style={{ width: "7rem" }} />
          <div className={styles.skeletonCell} style={{ width: "11rem" }} />
          <div className={styles.skeletonCell} style={{ width: "6rem" }} />
          <div className={styles.skeletonCell} style={{ width: "9rem" }} />
          <div className={styles.skeletonCell} style={{ width: "2rem" }} />
        </div>
      ))}
    </>
  );
}

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
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <div className={styles.headerIcon}>
            <UsersRound size={18} />
          </div>
          <div>
            <h1 className={styles.pageTitle}>User Management</h1>
            <p className={styles.pageSubtitle}>Manage system users and role assignments</p>
          </div>
        </div>
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
      </div>

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
              <div className={styles.emptyState}>
                <AlertCircle size={28} className={styles.emptyIcon} />
                <span className={styles.emptyTitle}>Failed to load users</span>
                <span className={styles.emptySubtitle}>{error.message}</span>
              </div>
            ) : users.length === 0 ? (
              <div className={styles.emptyState}>
                <UserX size={28} className={styles.emptyIcon} />
                <span className={styles.emptyTitle}>No users found</span>
              </div>
            ) : (
              users.map((user) => (
                <div key={user.id} className={`${styles.userRow} ${pendingUserId === user.id ? styles.pending : ""}`}>
                  <span className={styles.colName}>
                    <span className={styles.nameText}>
                      {user.firstName || user.lastName ? `${user.firstName} ${user.lastName ?? ""}`.trim() : "—"}
                    </span>
                    {isSelf(user.id) && <span className={styles.selfTag}>you</span>}
                    {user.isLockedOut && <span className={styles.lockedTag}>locked</span>}
                  </span>

                  <span className={styles.colEmail}>
                    <span className={styles.emailText}>{user.email}</span>
                  </span>

                  <span className={styles.colRoles}>
                    {user.roles.length > 0 ? (
                      user.roles.map((r) => <RoleBadge key={r} role={r} />)
                    ) : (
                      <span className={styles.noRole}>—</span>
                    )}
                  </span>

                  {isSovereign && (
                    <span className={styles.colAssign}>
                      <select
                        className={styles.roleSelect}
                        defaultValue=""
                        disabled={pendingUserId === user.id}
                        onChange={(e) => {
                          if (e.target.value) handleAssignRole(user.id, e.target.value as Role);
                          e.target.value = "";
                        }}
                      >
                        <option value="" disabled>
                          Add role…
                        </option>
                        {ROLE_OPTIONS.filter((r) => !user.roles.includes(r)).map((r) => (
                          <option key={r} value={r}>
                            {r}
                          </option>
                        ))}
                      </select>
                      {user.roles.map((r) => (
                        <button
                          key={r}
                          className={styles.removeRoleBtn}
                          title={`Remove ${r}`}
                          disabled={pendingUserId === user.id}
                          onClick={() => handleRemoveRole(user.id, r)}
                        >
                          <ShieldMinus size={11} />
                          {r}
                        </button>
                      ))}
                    </span>
                  )}

                  {isSovereign && (
                    <span className={styles.colActions}>
                      <button
                        className={styles.deleteBtn}
                        title="Delete user"
                        disabled={isSelf(user.id) || pendingUserId === user.id}
                        onClick={() => handleDeleteUser(user.id)}
                      >
                        <Trash2 size={13} />
                      </button>
                    </span>
                  )}
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
