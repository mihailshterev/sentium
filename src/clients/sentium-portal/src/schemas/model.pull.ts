import { z } from "zod";

export const modelPullSchema = z.object({
  modelName: z.string().min(1, "Model name is required"),
});

export type ModelPullFormData = z.infer<typeof modelPullSchema>;
