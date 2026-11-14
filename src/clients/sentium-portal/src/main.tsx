import "./app/zod-config";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./app/styles/fonts";
import "./app/styles/index.css";
import { App } from "./app/App.tsx";
import { AppProviders } from "./app/providers/app-providers";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <App />
    </AppProviders>
  </StrictMode>,
);
