import type { AUTH_STATUS } from "../utils/constants";

export type AuthStatus = (typeof AUTH_STATUS)[keyof typeof AUTH_STATUS];

export type User = {
  sub: string;
  email: string;
  name: string;
  roles: string[];
};
