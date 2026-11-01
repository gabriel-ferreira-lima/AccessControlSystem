import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// A porta e fixada para que a origem do frontend seja previsivel
// e possa ser liberada na politica de CORS do backend.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
  },
  preview: {
    port: 5173,
    strictPort: true,
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
    // Base da API fixa nos testes: um .env.local da máquina não pode mudar as URLs.
    env: { VITE_API_BASE_URL: "http://api.test" },
  },
});
