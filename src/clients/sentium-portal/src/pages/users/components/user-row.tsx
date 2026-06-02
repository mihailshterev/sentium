import { Trash2, ShieldMinus } from "lucide-react";
import styles from "../users.module.scss";
import type { UserListItem } from "../../../services/identity.service";
import type { Role } from "../../../utils/roles";
import RoleBadge from "./role-badge";

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

const AVATAR_COLORS = ["avatarBlue", "avatarPurple", "avatarGreen", "avatarAmber", "avatarCyan"] as const;

function getInitials(user: UserListItem): string {
  const first = user.firstName?.[0] ?? "";
  const last = user.lastName?.[0] ?? "";
  return (first + last).toUpperCase() || user.email[0].toUpperCase();
}

function getAvatarColor(id: string): string {
  return styles[AVATAR_COLORS[id.charCodeAt(0) % AVATAR_COLORS.length]];
}

const UserRow = ({
  user,
  isSelf,
  isSovereign,
  isPending,
  roleOptions,
  onAssignRole,
  onRemoveRole,
  onDeleteUser,
}: UserRowProps) => (
  <div className={`${styles.userRow} ${isPending ? styles.pending : ""}`}>
    <div className={`${styles.userAvatar} ${getAvatarColor(user.id)}`}>{getInitials(user)}</div>

    <div className={styles.colUser}>
      <div className={styles.nameRow}>
        <span className={styles.nameText}>
          {user.firstName || user.lastName ? `${user.firstName} ${user.lastName ?? ""}`.trim() : "—"}
        </span>
        {isSelf && <span className={styles.selfTag}>you</span>}
        {user.isLockedOut && <span className={styles.lockedTag}>locked</span>}
      </div>
      <span className={styles.emailText}>{user.email}</span>
    </div>

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
          disabled={isPending}
          onChange={(e) => {
            if (e.target.value) onAssignRole(user.id, e.target.value as Role);
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
        </select>
        {user.roles.map((r) => (
          <button
            key={r}
            className={styles.removeRoleBtn}
            title={`Remove ${r}`}
            disabled={isPending}
            onClick={() => onRemoveRole(user.id, r)}
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
          disabled={isSelf || isPending}
          onClick={() => onDeleteUser(user.id)}
        >
          <Trash2 size={13} />
        </button>
      </span>
    )}
  </div>
);

export default UserRow;
