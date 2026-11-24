import { z } from "zod";

/** Mirrors the server's password rules: at least 8 characters with a digit and a lowercase letter. */
export const newPasswordSchema = z
  .string()
  .min(8, "Password must be at least 8 characters")
  .max(256, "Password must be at most 256 characters")
  .regex(/\d/, "Include at least one digit")
  .regex(/[a-z]/, "Include at least one lowercase letter");
