import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import sassDts from "vite-plugin-sass-dts";
import path from "path";
import { fileURLToPath } from "url";

// Sentium.Web, for `pnpm dev`. Aspire passes the real URL; the fallback is its launch-profile HTTPS port.
const apiTarget = process.env.API_PROXY_TARGET ?? "https://localhost:7254";

// Same-origin in development too: the browser only talks to the Vite server, which forwards the API -
// so cookies, SSE streams and redirects behave exactly as in production.
const proxy = { target: apiTarget, changeOrigin: true, secure: false };

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react({
      compiler: true,
    }),
    sassDts({
      enabledMode: ["development", "production"],
      global: {
        generate: false,
        outputFilePath: path.resolve(import.meta.dirname, "./src/style.d.ts"),
      },
      sourceDir: path.resolve(import.meta.dirname, "./src"),
    }),
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    proxy: { "/api": proxy },
  },
  build: {
    outDir: path.resolve(import.meta.dirname, "../../hosts/Sentium.Web/wwwroot"),
    emptyOutDir: true,
    chunkSizeWarningLimit: 1600,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes("node_modules")) {
            return "vendor";
          }
        },
      },
    },
  },
});
