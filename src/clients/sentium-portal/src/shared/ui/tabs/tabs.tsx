import styles from "./tabs.module.scss";

export interface TabItem {
  id: string;
  label: string;
  icon?: React.ReactNode;
  count?: number;
}

interface TabsProps {
  tabs: TabItem[];
  active: string;
  onChange: (id: string) => void;
  className?: string;
}

const Tabs = ({ tabs, active, onChange, className }: TabsProps) => {
  return (
    <div className={`${styles.tabs} ${className ?? ""}`} role="tablist">
      {tabs.map((tab) => (
        <button
          key={tab.id}
          role="tab"
          aria-selected={active === tab.id}
          className={`${styles.tab} ${active === tab.id ? styles.active : ""}`}
          onClick={() => onChange(tab.id)}
        >
          {tab.icon && <span className={styles.icon}>{tab.icon}</span>}
          <span>{tab.label}</span>
          {tab.count != null && <span className={styles.count}>{tab.count}</span>}
        </button>
      ))}
    </div>
  );
};

export default Tabs;
