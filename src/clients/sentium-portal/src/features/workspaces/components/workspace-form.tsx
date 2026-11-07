import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { FolderOpen } from "lucide-react";
import styles from "../workspaces.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { workspaceFormSchema, type WorkspaceFormData } from "../schemas/workspace.form";

interface WorkspaceFormProps {
  initial?: { name: string; description: string };
  onSubmit: (name: string, description: string) => void;
  onCancel: () => void;
  isPending: boolean;
  title: string;
  error?: string | null;
}

export function WorkspaceForm({ initial, onSubmit, onCancel, isPending, title, error }: WorkspaceFormProps) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<WorkspaceFormData>({
    resolver: zodResolver(workspaceFormSchema),
    defaultValues: { name: initial?.name ?? "", description: initial?.description ?? "" },
  });

  return (
    <Dialog open onClose={onCancel} title={title} icon={<FolderOpen />} size="sm">
      <form onSubmit={handleSubmit((data) => onSubmit(data.name, data.description))} className={styles.form}>
        <FormField id="ws-name" label="Name" error={errors.name?.message}>
          <Input id="ws-name" placeholder="e.g. incident-2026" invalid={!!errors.name} {...register("name")} />
        </FormField>
        <FormField id="ws-desc" label="Description">
          <Textarea id="ws-desc" placeholder="Optional description…" rows={3} {...register("description")} />
        </FormField>
        {error && <StatusMessage variant="error" message={error} />}
        <div className={styles.formActions}>
          <Button variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={isPending}>
            {title}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
