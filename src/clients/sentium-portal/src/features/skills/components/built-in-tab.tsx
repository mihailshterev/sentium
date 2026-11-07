import { useState } from "react";
import { Cpu } from "lucide-react";
import styles from "../skills.module.css";
import { useSkills } from "@/features/skills/hooks/useSkills";
import { Badge } from "@/shared/ui/badge";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { SkillListSkeleton } from "./skill-list-skeleton";
import { SkillCard } from "./skill-card";

export function BuiltInTab() {
  const { builtInSkills, isBuiltInLoading } = useSkills();
  const [expanded, setExpanded] = useState<string | null>(null);

  return (
    <Card padding="none" as="section">
      <CardHeader
        icon={<Cpu />}
        title="Built-in Skills"
        description="Class-based skills always available to all agents — read-only"
        actions={<Badge tone="neutral">{builtInSkills.length} skills</Badge>}
      />

      {isBuiltInLoading ? (
        <SkillListSkeleton />
      ) : builtInSkills.length === 0 ? (
        <EmptyState icon={<Cpu />} title="No built-in skills found" />
      ) : (
        <ul className={styles.skillList}>
          {builtInSkills.map((skill) => (
            <SkillCard
              key={skill.name}
              skill={skill}
              expanded={expanded === skill.name}
              onToggle={() => setExpanded(expanded === skill.name ? null : skill.name)}
              pill="built-in"
              pillTone="info"
            />
          ))}
        </ul>
      )}
    </Card>
  );
}
