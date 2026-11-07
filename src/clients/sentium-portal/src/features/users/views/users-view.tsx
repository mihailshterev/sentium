import { useState } from "react";
import { UsersRound, RefreshCw, AlertCircle, UserX } from "lucide-react";
import styles from "../users.module.css";
import { ROLE_HIERARCHY, type Role } from "@/shared/utils/roles";
import { useRole } from "@/shared/auth/hooks/useRole";
import { useAuthStore } from "@/shared/auth/state/auth-store";
import { useUsers } from "@/features/users/hooks/useUsers";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { Page } from "@/shared/ui/page";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadMore } from "@/shared/ui/load-more";
import { SkeletonRows } from "../components/skeleton-rows";
import { UserRow } from "../components/user-row";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";

const ROLE_OPTIONS = ROLE_HIERARCHY as readonly Role[];

export function UsersView() {
  const [actionError, setActionError] = useState<string | null>(null);
  const [pendingUserId, setPendingUserId] = useState<string | null>(null);

  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const [userToDelete, setUserToDelete] = useState<{ id: string; identifier: string } | null>(null);

  const { isSovereign } = useRole();
  const currentUser = useAuthStore((s) => s.user);
  const {
    users,
    totalCount,
    hasMore,
    loadMore,
    isLoadingMore,
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

    const identifier = fullName || targetUser?.userName || "this user";

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
    <Page
      title="User Management"
      subtitle="Manage system users and role assignments"
      actions={
        <Button
          variant="secondary"
          icon={<RefreshCw size={15} className={isFetching ? "animate-spin" : undefined} />}
          onClick={() => refetch()}
          disabled={isFetching}
        >
          Refresh
        </Button>
      }
    >
      {actionError && (
        <div className={styles.actionError} role="alert">
          <AlertCircle size={16} aria-hidden="true" />
          <span>{actionError}</span>
          <Button variant="ghost" size="sm" onClick={() => setActionError(null)} aria-label="Dismiss error">
            ✕
          </Button>
        </div>
      )}

      <Card padding="none" as="section">
        <CardHeader
          icon={<UsersRound />}
          title="Registered users"
          actions={
            !isLoading && (
              <Badge tone="neutral">
                {totalCount} user{totalCount !== 1 ? "s" : ""}
              </Badge>
            )
          }
        />

        {isLoading ? (
          <SkeletonRows />
        ) : error ? (
          <EmptyState icon={<AlertCircle />} title="Failed to load users" hint={error.message} />
        ) : users.length === 0 ? (
          <EmptyState icon={<UserX />} title="No users found" />
        ) : (
          <div className="data-table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>User</th>
                  <th>Roles</th>
                  {isSovereign && <th>Assign / remove</th>}
                  {isSovereign && <th aria-label="Actions" />}
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
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
                ))}
              </tbody>
            </table>
          </div>
        )}

        <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
      </Card>

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
    </Page>
  );
}
