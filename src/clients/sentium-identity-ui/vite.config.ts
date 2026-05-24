import { defineConfig, loadEnv } from "vite";
import react, { reactCompilerPreset } from "@vitejs/plugin-react";
import babel from "@rolldown/plugin-babel";
import sassDts from "vite-plugin-sass-dts";
import path from "path";

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const identityApiBase = env.VITE_IDENTITY_API_BASE ?? "https://localhost:5001";

  return {
    plugins: [
      react(),
      babel({ presets: [reactCompilerPreset()] }),
      sassDts({
        enabledMode: ["development", "production"],
        global: {
          generate: false,
          outputFilePath: path.resolve(__dirname, "./src/style.d.ts"),
        },
        sourceDir: path.resolve(__dirname, "./src"),
      }),
    ],
    server: {
      port: 5174,
      proxy: {
        "/account": {
          target: identityApiBase,
          changeOrigin: true,
          secure: false,
        },
        "/connect": {
          target: identityApiBase,
          changeOrigin: true,
          secure: false,
        },
      },
    },
    build: {
      outDir: "../../services/Identity/Sentium.Identity.Api/wwwroot",
      emptyOutDir: true,
    },
  };
});
