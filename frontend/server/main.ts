// Starts the front server (see server.ts). Settings, from the environment:
//   PORT                  where it listens (default 8080: the container runs as a non-root user)
//   TONTUYAU_BACKEND_URL   the backend (default http://tontuyau-backend:8080, its name on the compose network)
//   TONTUYAU_DIST          the built app (default ../dist next to this file)
import { fileURLToPath } from 'node:url'
import { createFrontServer } from './server.ts'

const port = Number(process.env.PORT ?? 8080)
const backend = new URL(process.env.TONTUYAU_BACKEND_URL ?? 'http://tontuyau-backend:8080')
const root = process.env.TONTUYAU_DIST ?? fileURLToPath(new URL('../dist', import.meta.url))

if (!Number.isInteger(port) || port < 1 || port > 65535) {
  console.error(`PORT must be a port number (got "${process.env.PORT}").`)
  process.exit(2)
}
if (backend.protocol !== 'http:') {
  console.error(`TONTUYAU_BACKEND_URL must be an http:// URL (got "${backend.href}").`)
  process.exit(2)
}

const server = createFrontServer({ root, backend })
server.listen(port, () => {
  console.log(`TonTuyau front on :${port}, serving ${root}, backend ${backend.origin}`)
})

// docker stop: stop taking connections, let the current answers end, but don't wait for long video streams.
for (const signal of ['SIGTERM', 'SIGINT'] as const) {
  process.on(signal, () => {
    server.close(() => process.exit(0))
    server.closeIdleConnections()
    setTimeout(() => {
      server.closeAllConnections()
      process.exit(0)
    }, 5000).unref()
  })
}
