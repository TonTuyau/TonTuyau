import { readFileSync } from 'node:fs'
import { svelte } from '@sveltejs/vite-plugin-svelte'
import { svelteTesting } from '@testing-library/svelte/vite'
import { defineConfig } from 'vitest/config'
import { mockApi } from './mock/plugin.ts'

// Everything the browser needs from the backend goes through the same origin.
// `--mode mockup` (pnpm run dev:mockup): the dev server answers /api, /media, /art itself from mock/fixtures.
// Any other mode: they're proxied to the backend.
const backend = process.env.TONTUYAU_BACKEND_URL ?? 'http://localhost:5080'
const proxied = ['/api', '/media', '/art']
const port = Number(process.env.PORT) || 5173
// TonTuyau's version, shown in the settings menu; the same as the backend's (scripts/check-version.sh).
const version: string = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8')).version

export default defineConfig(({ mode }) => {
  const mockup = mode === 'mockup'
  return {
    plugins: [svelte(), svelteTesting(), ...(mockup ? [mockApi()] : [])],
    define: { __TONTUYAU_VERSION__: JSON.stringify(version) },
    server: {
      port,
      proxy: mockup
        ? undefined
        : Object.fromEntries(proxied.map((path) => [path, { target: backend, changeOrigin: false }])),
    },
    preview: { port },
    // Never inline a small asset as a data: URL: the Content-Security-Policy (server/server.ts) blocks them.
    build: { assetsInlineLimit: 0 },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      include: ['src/**/*.test.ts', 'mock/**/*.test.ts', 'server/**/*.test.ts', 'scripts/**/*.test.ts'],
      globals: true,
      restoreMocks: true,
      // Process CSS so theme files can be read as text (?raw) by the token test.
      css: { include: [/\/theme\//] },
    },
  }
})
