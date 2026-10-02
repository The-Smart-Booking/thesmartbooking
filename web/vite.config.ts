import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // /api vai para a API Go sem reescrever o caminho (docs/decisoes.md)
    proxy: { '/api': 'http://localhost:8080' },
  },
})
