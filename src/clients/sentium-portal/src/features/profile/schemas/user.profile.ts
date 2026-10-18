import { z } from "zod";

export const userProfileSchema = z.object({
  firstName: z.string().min(1, "First name is required").max(100),
  lastName: z.string().max(100),
  email: z.email("Invalid email address").max(256),
});

export type UserProfileFormData = z.infer<typeof userProfileSchema>;
