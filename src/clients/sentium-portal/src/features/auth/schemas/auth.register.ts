import { z } from "zod";

export const registerSchema = z.object({
  userName: z
    .string()
    .trim()
    .min(3, "Username must be at least 3 characters")
    .max(64, "Username must be at most 64 characters")
    .regex(/^[A-Za-z0-9._@+-]+$/, "Use only letters, numbers and . _ - @ +"),
  password: z.string().min(8, "Password must be at least 8 characters"),
});

export type RegisterFormData = z.infer<typeof registerSchema>;
