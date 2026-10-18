import styles from "../users.module.scss";
import type { Role } from "../../../shared/utils/roles";

export function RoleBadge({ role }: { role: Role }) {
  return <span className={`${styles.roleBadge} ${styles[`role_${role.toLowerCase()}`]}`}>{role}</span>;
}
