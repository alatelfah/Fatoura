import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The API runs on :5080 in development; proxying keeps the refresh-token cookie same-origin.
const api = process.env.FATOURA_API ?? 'http://localhost:5080';

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, proxy: { '/api': { target: api, changeOrigin: false } } },
  preview: { port: 4173, proxy: { '/api': { target: api, changeOrigin: false } } },
  build: { chunkSizeWarningLimit: 2000 },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
});
