import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  resolve: { alias: { "@": path.resolve(__dirname, "src") } },
  server: {
    proxy: {
      "/api": {
        target: process.env.VITE_API_PROXY || "https://localhost:7096",
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
