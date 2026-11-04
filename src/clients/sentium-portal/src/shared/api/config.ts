// The portal is always same-origin with the API: Sentium.Web serves the built client, and the Vite dev
// server proxies /api and /bff to it (see vite.config.ts).
export const BASE_URL = "/api";
export const BFF_BASE = "/bff";
