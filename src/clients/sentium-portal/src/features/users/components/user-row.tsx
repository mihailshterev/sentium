import { ShieldMinus, Trash2 } from "lucide-react";
import styles from "../users.module.css";
import type { UserListItem } from "../../auth/api/identity.service";
import type { Role } from "../../../shared/utils/roles";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Select } from "@/shared/ui/form-controls";
import { RoleBadge } from "./role-badge";

interface UserRowProps {
  user: UserListItem;
  isSelf: boolean;
  isSovereign: boolean;
  isPending: boolean;
  roleOptions: readonly Role[];
  onAssignRole: (userId: string, role: Role) => void;
  onRemoveRole: (userId: string, role: Role) => void;
  onDeleteUser: (userId: string) => void;
}

function getInitials(user: UserListItem): string {
  const first = user.firstName?.[0] ?? "";
  const last = user.lastName?.[0] ?? "";
  return (first + last).toUpperCase() || user.userName[0].toUpperCase();
}

export function UserRow({
  user,
  isSelf,
  isSovereign,
  isPending,
  roleOptions,
  onAssignRole,
  onRemoveRole,
  onDeleteUser,
}: UserRowProps) {
  const fullName = user.firstName || user.lastName ? `${user.firstName} ${user.lastName ?? ""}`.trim() : "—";

  return (
    <tr className={styles.row} data-pending={isPending || undefined}>
      <td>
        <div className={styles.userCell}>
          <span className={styles.avatar} aria-hidden="true">
            {getInitials(user)}
          </span>
          <div className={styles.userText}>
            <div className={styles.nameRow}>
              <span className={styles.name}>{fullName}</span>
              {isSelf && <Badge tone="accent">you</Badge>}
              {user.isLockedOut && <Badge tone="danger">locked</Badge>}
            </div>
            <span className={styles.username}>{user.userName}</span>
          </div>
        </div>
      </td>

      <td>
        <div className={styles.roles}>
          {user.roles.length > 0 ? (
            user.roles.map((r) => <RoleBadge key={r} role={r} />)
          ) : (
            <span className={styles.noRole}>—</span>
          )}
        </div>
      </td>

      {isSovereign && (
        <td>
          <div className={styles.assign}>
            <Select
              controlSize="sm"
              defaultValue=""
              disabled={isPending}
              aria-label={`Add role to ${user.userName}`}
              wrapperClassName={styles.roleSelect}
              onChange={(e) => {
                if (e.target.value) {
                  onAssignRole(user.id, e.target.value as Role);
                }
                e.target.value = "";
              }}
            >
              <option value="" disabled>
                Add role…
              </option>
              {roleOptions
                .filter((r) => !user.roles.includes(r))
                .map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
            </Select>
            {user.roles.map((r) => (
              <Button
                key={r}
                variant="ghost"
                size="sm"
                icon={<ShieldMinus size={13} />}
                title={`Remove ${r}`}
                disabled={isPending}
                onClick={() => onRemoveRole(user.id, r)}
                className={styles.removeRole}
              >
                {r}
              </Button>
            ))}
          </div>
        </td>
      )}

      {isSovereign && (
        <td className={styles.actions}>
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<Trash2 size={15} />}
            title="Delete user"
            disabled={isSelf || isPending}
            onClick={() => onDeleteUser(user.id)}
            className={styles.deleteButton}
          />
        </td>
      )}
    </tr>
  );
}
