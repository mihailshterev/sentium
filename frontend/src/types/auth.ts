export type AuthStatus = "idle" | "checking" | "authenticated" | "unauthenticated";

export type User = {
  sub: string;
  email: string;
  name: string;
  roles: string[];
};
