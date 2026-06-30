import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// In dev, proxy /api to the .NET API so there are no CORS concerns.
// Override the target with VITE_API_PROXY when the API runs elsewhere.
const apiTarget = process.env.VITE_API_PROXY || 'http://localhost:5099'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
    },
  },
})
