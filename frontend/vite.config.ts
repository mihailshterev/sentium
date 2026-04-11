import { defineConfig } from "vite";
import react, { reactCompilerPreset } from "@vitejs/plugin-react";
import sassDts from "vite-plugin-sass-dts";
import babel from "@rolldown/plugin-babel";
import path from "path";

// https://vite.dev/config/
export default defineConfig({
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
});
