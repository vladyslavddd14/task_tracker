import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// During `npm run dev` the API is proxied, so the browser talks to a single origin (like nginx in Docker).
// Point VITE_API_PROXY to another address if the API does not run on http://localhost:8080.
const apiTarget = process.env.VITE_API_PROXY ?? 'http://localhost:8080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
});
