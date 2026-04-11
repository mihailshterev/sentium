export const API_BASE = import.meta.env.VITE_API_BASE + "/api";

export const BFF_BASE = import.meta.env.VITE_API_BASE;

// will be refactored into separate API utility module and specific services for the different functional areas
export const apiFetch = (input: string | URL | Request, init?: RequestInit): Promise<Response> =>
  fetch(input, { credentials: "include", ...init });
