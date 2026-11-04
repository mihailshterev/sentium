import { z } from "zod";

export const userProfileSchema = z.object({
  firstName: z.string().min(1, "First name is required").max(100),
  lastName: z.string().max(100),
  userName: z
    .string()
    .trim()
    .min(3, "Username must be at least 3 characters")
    .max(64)
    .regex(/^[A-Za-z0-9._@+-]+$/, "Use only letters, numbers and . _ - @ +"),
});

export type UserProfileFormData = z.infer<typeof userProfileSchema>;
