import { defineConfig } from "vite";
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
});
