import { z } from "zod";

export const workflowEditorSchema = z.object({
  name: z.string().min(1, "Workflow name is required").max(255),
  description: z.string().max(4000),
});

export type WorkflowEditorFormData = z.infer<typeof workflowEditorSchema>;
