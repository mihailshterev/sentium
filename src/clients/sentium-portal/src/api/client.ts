import { useAuthStore } from "../stores/auth-store";
import { AUTH_STATUS } from "../utils/constants";

export const BASE_URL = import.meta.env.VITE_API_BASE + "/api";

export class ApiError extends Error {
  readonly status: number;
  readonly errors?: Record<string, string[]>;

  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.errors = errors;
  }

  get isNotFound() {
    return this.status === 404;
  }
  get isConflict() {
    return this.status === 409;
  }
  get isValidation() {
    return this.status === 400;
  }
}

export const BFF_BASE = import.meta.env.VITE_API_BASE + "/bff";

interface RequestOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
}

async function request<T>(endpoint: string, options: RequestOptions = {}): Promise<T> {
  const { body, ...restOptions } = options;

  const config: RequestInit = {
    ...restOptions,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
  };

  if (body) {
    config.body = JSON.stringify(body);
  }

  const response = await fetch(`${BASE_URL}${endpoint}`, config);

  if (response.status === 401) {
    useAuthStore.setState({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });

    if (!window.location.pathname.includes("/login")) {
      window.location.href = `${BFF_BASE}/login?returnUrl=${encodeURIComponent(window.location.pathname)}`;
    }

    throw new Error("Session expired. Please log in again.");
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message: string = errorData.detail ?? errorData.title ?? `HTTP error! status: ${response.status}`;
    throw new ApiError(response.status, message, errorData.errors);
  }

  if (response.status === 204 || response.headers.get("Content-Length") === "0") {
    return {} as T;
  }

  return response.json().catch(() => ({}) as T);
}

export const client = {
  get: <T>(url: string, options?: RequestOptions) => request<T>(url, { ...options, method: "GET" }),

  post: <T>(url: string, body: unknown, options?: RequestOptions) =>
    request<T>(url, { ...options, method: "POST", body }),

  put: <T>(url: string, body: unknown, options?: RequestOptions) =>
    request<T>(url, { ...options, method: "PUT", body }),

  delete: <T>(url: string, options?: RequestOptions) => request<T>(url, { ...options, method: "DELETE" }),
};
