import styles from "../users.module.scss";
import type { Role } from "../../../utils/roles";

const RoleBadge = ({ role }: { role: Role }) => (
  <span className={`${styles.roleBadge} ${styles[`role_${role.toLowerCase()}`]}`}>{role}</span>
);

export default RoleBadge;
