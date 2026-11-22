import { handleUnauthorized } from "@/shared/auth/unauthorized";
import { BASE_URL } from "./config";
export { BASE_URL } from "./config";
export { handleUnauthorized } from "@/shared/auth/unauthorized";

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

interface RequestOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
}

async function request<T>(endpoint: string, options: RequestOptions = {}): Promise<T> {
  const { body, ...restOptions } = options;
  const isForm = typeof FormData !== "undefined" && body instanceof FormData;

  const config: RequestInit = {
    ...restOptions,
    credentials: "include",
    headers: isForm
      ? { ...options.headers }
      : {
          "Content-Type": "application/json",
          ...options.headers,
        },
  };

  // `false`, `0` and `""` are valid JSON bodies; only a missing body is left out.
  if (body !== undefined && body !== null) {
    config.body = isForm ? (body as FormData) : JSON.stringify(body);
  }

  const response = await fetch(`${BASE_URL}${endpoint}`, config);

  if (response.status === 401) {
    handleUnauthorized();
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message: string = errorData.detail ?? errorData.title ?? `HTTP error! status: ${response.status}`;
    throw new ApiError(response.status, message, errorData.errors);
  }

  if (response.status === 204 || response.headers.get("Content-Length") === "0") {
    return {} as T;
  }

  try {
    return (await response.json()) as T;
  } catch {
    // A body that claims to be JSON but is not is a server fault, not an empty result.
    if (response.headers.get("Content-Type")?.includes("json")) {
      throw new ApiError(response.status, "The server sent a malformed response.");
    }
    return {} as T;
  }
}

export const client = {
  get: <T>(url: string, options?: RequestOptions) => request<T>(url, { ...options, method: "GET" }),

  post: <T>(url: string, body: unknown, options?: RequestOptions) =>
    request<T>(url, { ...options, method: "POST", body }),

  put: <T>(url: string, body: unknown, options?: RequestOptions) =>
    request<T>(url, { ...options, method: "PUT", body }),

  delete: <T>(url: string, options?: RequestOptions) => request<T>(url, { ...options, method: "DELETE" }),

  postForm: <T>(url: string, form: FormData, options?: RequestOptions) =>
    request<T>(url, { ...options, method: "POST", body: form }),
};
