import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": { target: "https://localhost:7284", changeOrigin: true, secure: false },
    },
  },
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
    coverage: { reporter: ["text", "html", "cobertura"], include: ["src/**/*.{ts,tsx}"] },
  },
});
