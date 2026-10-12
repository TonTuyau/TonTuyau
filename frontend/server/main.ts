// Starts the front server (see server.ts). Settings, from the environment:
//   PORT                  where it listens (default 8080: the container runs as a non-root user)
//   TONTUYAU_BACKEND_URL   the backend (default http://tontuyau-backend:8080, its name on the compose network)
//   TONTUYAU_DIST          the built app (default ../dist next to this file)
//   TONTUYAU_PUBLIC_URL    the address people reach TonTuyau at, e.g. https://tontuyau.example.net (default none: no
//                          link previews)
import { fileURLToPath } from 'node:url'
import { createFrontServer } from './server.ts'

const port = Number(process.env.PORT ?? 8080)
const backend = new URL(process.env.TONTUYAU_BACKEND_URL ?? 'http://tontuyau-backend:8080')
const root = process.env.TONTUYAU_DIST ?? fileURLToPath(new URL('../dist', import.meta.url))
const publicUrl = parsePublicUrl(process.env.TONTUYAU_PUBLIC_URL ?? '')

/** An http(s) origin, nothing after it (a trailing slash is fine); undefined when empty; exits when wrong. */
function parsePublicUrl(value: string): URL | undefined {
  if (value === '') {
    return undefined
  }
  const url = URL.canParse(value) ? new URL(value) : null
  if (url === null || !['http:', 'https:'].includes(url.protocol) || url.pathname !== '/' || url.search || url.hash) {
    console.error(`TONTUYAU_PUBLIC_URL must be an address like https://tontuyau.example.net (got "${value}").`)
    process.exit(2)
  }
  return url
}

if (!Number.isInteger(port) || port < 1 || port > 65535) {
  console.error(`PORT must be a port number (got "${process.env.PORT}").`)
  process.exit(2)
}
if (backend.protocol !== 'http:') {
  console.error(`TONTUYAU_BACKEND_URL must be an http:// URL (got "${backend.href}").`)
  process.exit(2)
}

const server = createFrontServer({ root, backend, publicUrl })
server.listen(port, () => {
  const previews = publicUrl ? `link previews for ${publicUrl.origin}` : 'no link previews (no TONTUYAU_PUBLIC_URL)'
  console.log(`TonTuyau front on :${port}, serving ${root}, backend ${backend.origin}, ${previews}`)
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
