import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    {
      name: 'silence-missing-sourcemaps',
      apply: 'serve' as const,
      configureServer(server) {
        server.middlewares.use((req, res, next) => {
          if (req.url?.endsWith('.map') && req.url.includes('node_modules')) {
            res.setHeader('Content-Type', 'application/json')
            res.end('{"version":3,"sources":[],"mappings":""}')
            return
          }
          next()
        })
      },
    },
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    sourcemapIgnoreList: (sourcePath) => sourcePath.includes('node_modules'),
    proxy: {
      '/account': { target: 'https://localhost:7070', secure: false },
      '/connect': { target: 'https://localhost:7070', secure: false },
      '/api': { target: 'https://localhost:7070', secure: false },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
  },
})
