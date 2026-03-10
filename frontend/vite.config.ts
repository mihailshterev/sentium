import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import sassDts from "vite-plugin-sass-dts";
import path from "path";

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react({
      babel: {
        plugins: [["babel-plugin-react-compiler"]],
      },
    }),
    sassDts({
      enabledMode: ["development", "production"],
      global: {
        generate: true,
        outputFilePath: path.resolve(__dirname, "./src/style.d.ts"),
      },
      sourceDir: path.resolve(__dirname, "./src"),
    }),
  ],
});
