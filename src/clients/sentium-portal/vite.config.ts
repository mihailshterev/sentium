import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";
import { fileURLToPath } from "url";

// Sentium.Web, for `pnpm dev`. Aspire passes the real URL; the fallback is its launch-profile HTTPS port.
const apiTarget = process.env.API_PROXY_TARGET ?? "https://localhost:7254";

// Same-origin in development too: the browser only talks to the Vite server, which forwards the API -
// so cookies, SSE streams and redirects behave exactly as in production.
const proxy = { target: apiTarget, changeOrigin: true, secure: false };

const MAP_ONLY_PACKAGES = /node_modules\/(d3-[a-z-]+)\//;

export default defineConfig({
  plugins: [
    react({
      compiler: true,
    }),
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  css: {
    transformer: "lightningcss",
  },
  server: {
    proxy: { "/api": proxy },
  },
  build: {
    outDir: path.resolve(import.meta.dirname, "../../hosts/Sentium.Web/wwwroot"),
    emptyOutDir: true,
    chunkSizeWarningLimit: 1600,
    // Fonts stay separate files: the Content-Security-Policy's font-src allows the portal's origin, not data: URIs.
    assetsInlineLimit: (file) => (/\.woff2?$/.test(file) ? false : undefined),
    rollupOptions: {
      output: {
        // d3-force is only used by the lazily loaded semantic map and vault graph. Leaving it out of the shared
        // vendor chunk lets the bundler place it with those pages, off the initial load. (Naming it as a manual
        // chunk instead pulls shared runtime helpers into it, which makes every page load it.)
        manualChunks(id) {
          if (!id.includes("node_modules") || MAP_ONLY_PACKAGES.test(id)) {
            return undefined;
          }
          return "vendor";
        },
      },
    },
  },
});
