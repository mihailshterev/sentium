import styles from "./status-message.module.scss";

type StatusMessageVariant = "success" | "error" | "loading";

interface StatusMessageProps {
  variant: StatusMessageVariant;
  icon?: React.ReactNode;
  message: string;
}

const VARIANT_CLASS: Record<StatusMessageVariant, string> = {
  success: styles.success,
  error: styles.error,
  loading: styles.loading,
};

const StatusMessage = ({ variant, icon, message }: StatusMessageProps) => {
  return (
    <div className={`${styles.msg} ${VARIANT_CLASS[variant]}`}>
      {icon}
      <span>{message}</span>
    </div>
  );
};

export default StatusMessage;
