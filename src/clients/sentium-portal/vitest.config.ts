import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "path";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  define: {
    "import.meta.env.VITE_API_BASE": JSON.stringify("http://localhost:5000"),
    "import.meta.env.MODE": JSON.stringify("test"),
    "import.meta.env.DEV": JSON.stringify(false),
    "import.meta.env.PROD": JSON.stringify(false),
    "import.meta.env.SSR": JSON.stringify(false),
  },
  css: {
    modules: {
      generateScopedName: "[local]",
    },
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/setupTests.ts"],
    clearMocks: true,
    typecheck: {
      tsconfig: "./tsconfig.test.json",
    },
    include: ["src/**/*.{test,spec}.{ts,tsx}"],
    exclude: ["node_modules", "dist"],
    coverage: {
      provider: "v8",
      reporter: ["text", "json", "lcov", "html"],
      reportsDirectory: "./coverage",
      include: ["src/**/*.{ts,tsx}"],
      exclude: [
        "src/**/*.test.{ts,tsx}",
        "src/**/*.d.ts",
        "src/main.tsx",
        "src/vite-env.d.ts",
        "src/**/*.module.scss",
        "src/**/*.module.d.scss.ts",
        "src/types/**",
        "src/pages/login/animated-bg.tsx",
        "src/components/ui/animated-bg.tsx",
        "src/components/ui/aurora-background.tsx",
        "src/pages/semantic-map/engine/renderer.ts",
        "src/pages/semantic-map/semantic-map.tsx",
        "src/pages/semantic-map/hud.tsx",
      ],
      thresholds: {
        statements: 88,
        branches: 78,
        functions: 83,
        lines: 89,
      },
    },
    reporters: ["verbose"],
    testTimeout: 10_000,
    hookTimeout: 10_000,
  },
});
