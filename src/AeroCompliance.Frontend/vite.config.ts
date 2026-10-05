import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig(() => {
  const apiProxy = process.env.VITE_API_PROXY;
  return {
    plugins: [react()],
    resolve: { alias: { "@": path.resolve(__dirname, "src") } },
    server: apiProxy
      ? {
          proxy: {
            "/api": { target: apiProxy, changeOrigin: true, secure: false },
          },
        }
      : undefined,
  };
});
