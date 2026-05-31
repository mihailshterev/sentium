import { useState } from "react";
import { ChevronDown, ChevronUp, Cpu, Loader } from "lucide-react";
import styles from "../skills.module.scss";
import { useSkills } from "../../../hooks/useSkills";
import type { BuiltInSkill } from "../../../types/skills";
import EmptyState from "../../../components/ui/empty-state";

const BuiltInTab = () => {
  const { builtInSkills, isBuiltInLoading } = useSkills();
  const [expanded, setExpanded] = useState<string | null>(null);

  return (
    <div className={styles.card}>
      <div className={styles.cardHeader}>
        <div className={styles.cardHeaderLeft}>
          <Cpu size={15} className={styles.cardIconCyan} />
          <div>
            <p className={styles.cardTitle}>Built-in Skills</p>
            <p className={styles.cardSubtitle}>Class-based skills always available to all agents — read-only</p>
          </div>
        </div>
        <span className={styles.pillCyan}>{builtInSkills.length} skills</span>
      </div>

      <div className={styles.cardBody}>
        {isBuiltInLoading && (
          <p className={styles.loadingText}>
            <Loader size={14} className="animate-spin" />
            Loading skills…
          </p>
        )}

        {!isBuiltInLoading && builtInSkills.length === 0 && (
          <EmptyState icon={<Cpu size={32} />} title="No built-in skills found" />
        )}

        <div className={styles.skillList}>
          {builtInSkills.map((skill: BuiltInSkill) => (
            <div key={skill.name} className={styles.skillRow}>
              <div className={styles.skillRowHeader}>
                <div className={styles.skillMeta}>
                  <span className={styles.skillName}>{skill.name}</span>
                  <span className={styles.pillCyan}>built-in</span>
                </div>
                <button
                  className={styles.btnIcon}
                  onClick={() => setExpanded(expanded === skill.name ? null : skill.name)}
                  title="Toggle instructions"
                >
                  {expanded === skill.name ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                </button>
              </div>
              <p className={styles.skillDesc}>{skill.description}</p>
              {expanded === skill.name && <pre className={styles.instructions}>{skill.instructions}</pre>}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default BuiltInTab;
