import { useAuthStore } from "../stores/auth-store";

/* eslint-disable @typescript-eslint/no-explicit-any */
export const BASE_URL = import.meta.env.VITE_API_BASE + "/api";

export const BFF_BASE = import.meta.env.VITE_API_BASE + "/bff";

interface RequestOptions extends RequestInit {
  body?: any;
}

async function request<T>(endpoint: string, options: RequestOptions = {}): Promise<T> {
  const config: RequestInit = {
    ...options,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
  };

  if (options.body) {
    config.body = JSON.stringify(options.body);
  }

  const response = await fetch(`${BASE_URL}${endpoint}`, config);

  if (response.status === 401) {
    useAuthStore.getState().logout();

    if (!window.location.pathname.includes("/login")) {
      window.location.href = `${BFF_BASE}/login?returnUrl=${encodeURIComponent(window.location.pathname)}`;
    }

    throw new Error("Session expired. Please log in again.");
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error(errorData.message || `HTTP error! status: ${response.status}`);
  }

  if (response.status === 204 || response.headers.get("Content-Length") === "0") {
    return {} as T;
  }

  return response.json().catch(() => ({}) as T);
}

export const client = {
  get: <T>(url: string, options?: RequestOptions) => request<T>(url, { ...options, method: "GET" }),

  post: <T>(url: string, body: any, options?: RequestOptions) => request<T>(url, { ...options, method: "POST", body }),

  put: <T>(url: string, body: any, options?: RequestOptions) => request<T>(url, { ...options, method: "PUT", body }),

  delete: <T>(url: string, options?: RequestOptions) => request<T>(url, { ...options, method: "DELETE" }),
};
