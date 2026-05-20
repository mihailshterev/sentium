import styles from "../users.module.scss";

const RoleBadge = ({ role }: { role: string }) => (
  <span className={`${styles.roleBadge} ${styles[`role_${role.toLowerCase()}`]}`}>{role}</span>
);

export default RoleBadge;
