import { z } from "zod";

export const workspaceFormSchema = z.object({
  name: z.string().min(1, "Name is required"),
  description: z.string(),
});

export type WorkspaceFormData = z.infer<typeof workspaceFormSchema>;
