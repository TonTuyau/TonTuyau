// TonTuyau's production front server (QA13-9 a): plain `node:http`, no dependency. Runs in the front container.
// - Serves the built app (dist/): the hashed files under /assets/ are cached for a year, everything else is
//   revalidated (ETag); any other path that isn't a file gets index.html, and the client-side router decides.
// - Forwards /api, /media and /art to the backend, streaming both ways: Range, If-None-Match, If-Range… go up,
//   the status (200, 206, 304, 416…) and headers come back, and a client that goes away cancels the upstream request.
//   A backend that can't be reached gives a bare 502 (plain text, not ProblemDetails: the front then says "Can't reach
//   the TonTuyau server", as with the dev server's proxy).
// - With a public address (TONTUYAU_PUBLIC_URL), a page gets its link preview's tags, and /oembed answers (see
//   previews.ts); such a page is written for its URL, so it's revalidated without an ETag.
// - Every answer carries the security headers below, the Content-Security-Policy included (added to the backend's
//   answers when it didn't set them).
import { createReadStream } from 'node:fs'
import { readFile, stat } from 'node:fs/promises'
import http, {
  type IncomingHttpHeaders,
  type IncomingMessage,
  type OutgoingHttpHeaders,
  type ServerResponse,
} from 'node:http'
import path from 'node:path'
import { pipeline } from 'node:stream'
import { oembed, previewTags, withTags, type PreviewOptions } from './previews.ts'

export interface FrontServerOptions {
  /** The built app (Vite's dist/), absolute or relative to the working directory. */
  root: string
  /** The backend's base URL, e.g. http://tontuyau-backend:8080. */
  backend: URL
  /** Where failures are reported (a proxy error, a file that can't be read). */
  log?: (message: string) => void
  /** The public address, for the link previews (none without it). */
  publicUrl?: URL
}

export const proxiedPrefixes = ['/api', '/media', '/art'] as const

/** Hashed by Vite: the name changes with the content, so the file can be cached forever. */
const IMMUTABLE = 'public, max-age=31536000, immutable'
const REVALIDATE = 'no-cache'

/**
 * The Content-Security-Policy (QA14-3: built as if TonTuyau were already public). Everything comes from this origin,
 * images included: no data: URLs (QA15-2 b), nothing uses them and vite.config.ts never inlines an asset as one.
 * No inline script: index.html loads its theme script as a file (public/theme-init.js). No inline style either: the
 * app's few dynamic styles (the player's aspect ratio, the description's line count) are CSS custom properties that
 * Svelte sets through the CSSOM (element.style), which a CSP doesn't restrict, so neither 'unsafe-inline' nor
 * style-src-attr is needed. Media from blob: too (2.0, C3): a video played in parts plays through a MediaSource, whose
 * address the page makes itself (URL.createObjectURL); hls.js runs without a worker, so no worker-src.
 */
export const contentSecurityPolicy = [
  "default-src 'self'",
  "script-src 'self'",
  "style-src 'self'",
  "img-src 'self'",
  "media-src 'self' blob:",
  "connect-src 'self'",
  "font-src 'self'",
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'self'",
].join('; ')

const securityHeaders: Record<string, string> = {
  'content-security-policy': contentSecurityPolicy,
  'x-content-type-options': 'nosniff',
  'referrer-policy': 'same-origin',
  'x-frame-options': 'SAMEORIGIN',
}

const contentTypes: Record<string, string> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8',
  '.txt': 'text/plain; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.avif': 'image/avif',
  '.gif': 'image/gif',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
}

/** Headers about one connection, never forwarded (RFC 9110 §7.6.1), plus whatever `Connection` names. */
const hopByHop = new Set([
  'connection',
  'keep-alive',
  'proxy-authenticate',
  'proxy-authorization',
  'proxy-connection',
  'te',
  'trailer',
  'transfer-encoding',
  'upgrade',
])

function endToEnd(headers: IncomingHttpHeaders): OutgoingHttpHeaders {
  const named = new Set(
    String(headers.connection ?? '')
      .split(',')
      .map((name) => name.trim().toLowerCase())
      .filter(Boolean),
  )
  const kept: OutgoingHttpHeaders = {}
  for (const [name, value] of Object.entries(headers)) {
    if (value !== undefined && !hopByHop.has(name) && !named.has(name)) {
      kept[name] = value
    }
  }
  return kept
}

export function isProxied(pathname: string): boolean {
  return proxiedPrefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`))
}

function sendText(res: ServerResponse, status: number, text: string, extra: OutgoingHttpHeaders = {}): void {
  const body = `${text}\n`
  res.writeHead(status, {
    ...securityHeaders,
    'content-type': 'text/plain; charset=utf-8',
    'content-length': Buffer.byteLength(body),
    'cache-control': 'no-store',
    ...extra,
  })
  res.end(res.req.method === 'HEAD' ? undefined : body)
}

export function createFrontServer(options: FrontServerOptions): http.Server {
  const root = path.resolve(options.root)
  const index = path.join(root, 'index.html')
  const backend = options.backend
  const log = options.log ?? ((message: string) => console.error(message))
  const agent = new http.Agent({ keepAlive: true })
  const previews: PreviewOptions | null = options.publicUrl ? { backend, publicUrl: options.publicUrl, log } : null

  function forward(req: IncomingMessage, res: ServerResponse): void {
    const headers = endToEnd(req.headers)
    const client = req.socket.remoteAddress ?? ''
    const forwardedFor = req.headers['x-forwarded-for']
    headers['x-forwarded-for'] = forwardedFor ? `${forwardedFor}, ${client}` : client
    headers['x-forwarded-proto'] = 'http'
    if (req.headers.host) {
      headers['x-forwarded-host'] = req.headers.host
    }

    const upstream = http.request({
      protocol: backend.protocol,
      hostname: backend.hostname,
      port: backend.port || 80,
      method: req.method,
      path: req.url,
      headers,
      agent,
    })

    // The browser went away (seeking, closing the tab) before the answer ended: stop asking the backend.
    res.on('close', () => {
      if (!res.writableFinished) {
        upstream.destroy()
      }
    })

    upstream.on('response', (answer) => {
      const outgoing = endToEnd(answer.headers)
      for (const [name, value] of Object.entries(securityHeaders)) {
        outgoing[name] ??= value
      }
      res.writeHead(answer.statusCode ?? 502, answer.statusMessage, outgoing)
      pipeline(answer, res, (error) => {
        if (error) {
          upstream.destroy()
        }
      })
    })

    upstream.on('error', (error) => {
      if (res.headersSent) {
        // Mid-answer: all we can do is cut the connection so the browser sees an incomplete answer.
        res.destroy()
        return
      }
      if (!res.destroyed) {
        log(`proxy: ${req.method} ${req.url}: ${error.message}`)
        sendText(res, 502, 'Bad Gateway: the TonTuyau backend can’t be reached.')
      }
    })

    // The request body (if any) goes up as it arrives.
    pipeline(req, upstream, () => {
      // Errors surface on `upstream` ('error' above) or as the client closing ('close' above).
    })
  }

  /** The file under root that `pathname` names, or null when it can't be one (traversal, hidden, malformed). */
  function fileFor(pathname: string): string | null {
    let decoded: string
    try {
      decoded = decodeURIComponent(pathname)
    } catch {
      return null
    }
    if (decoded.includes('\0') || decoded.includes('\\')) {
      return null
    }
    const segments = decoded.split('/')
    // No "..", and no hidden file: Vite's output has none.
    if (segments.some((segment) => segment.startsWith('.'))) {
      return null
    }
    const file = path.resolve(root, `.${path.posix.normalize(decoded)}`)
    // root itself ("/") isn't a file: it falls through to index.html like every page.
    return file === root || file.startsWith(root + path.sep) ? file : null
  }

  async function serveFile(req: IncomingMessage, res: ServerResponse, file: string, cache: string): Promise<boolean> {
    const info = await stat(file).catch(() => null)
    if (!info?.isFile()) {
      return false
    }
    const etag = `W/"${info.size.toString(16)}-${Math.floor(info.mtimeMs).toString(16)}"`
    const headers: OutgoingHttpHeaders = {
      ...securityHeaders,
      'content-type': contentTypes[path.extname(file).toLowerCase()] ?? 'application/octet-stream',
      'cache-control': cache,
      etag,
      'last-modified': info.mtime.toUTCString(),
    }
    const ifNoneMatch = req.headers['if-none-match']
    if (ifNoneMatch && ifNoneMatch.split(',').some((tag) => tag.trim() === etag || tag.trim() === '*')) {
      res.writeHead(304, headers)
      res.end()
      return true
    }
    headers['content-length'] = info.size
    res.writeHead(200, headers)
    if (req.method === 'HEAD') {
      res.end()
      return true
    }
    pipeline(createReadStream(file), res, (error) => {
      if (error && !res.destroyed) {
        log(`static: ${file}: ${error.message}`)
        res.destroy()
      }
    })
    return true
  }

  function sendBody(req: IncomingMessage, res: ServerResponse, status: number, type: string, body: string): void {
    res.writeHead(status, {
      ...securityHeaders,
      'content-type': type,
      'content-length': Buffer.byteLength(body),
      'cache-control': REVALIDATE,
    })
    res.end(req.method === 'HEAD' ? undefined : body)
  }

  /** index.html with the link preview of the page asked. */
  async function servePage(req: IncomingMessage, res: ServerResponse, preview: PreviewOptions, target: URL) {
    const html = await readFile(index, 'utf8').catch(() => null)
    if (html === null) {
      log(`static: ${index} is missing`)
      sendText(res, 500, 'The front has no index.html: was it built?')
      return
    }
    const tags = await previewTags(preview, target.pathname, target.searchParams)
    sendBody(req, res, 200, contentTypes['.html'] ?? 'text/html', withTags(html, tags))
  }

  async function serveOembed(req: IncomingMessage, res: ServerResponse, preview: PreviewOptions, target: URL) {
    const document = await oembed(preview, target.searchParams)
    if (document === null) {
      sendText(res, 404, 'Not Found')
      return
    }
    sendBody(req, res, 200, 'application/json; charset=utf-8', JSON.stringify(document))
  }

  async function serveStatic(req: IncomingMessage, res: ServerResponse, pathname: string): Promise<void> {
    if (req.method !== 'GET' && req.method !== 'HEAD') {
      sendText(res, 405, 'Method Not Allowed', { allow: 'GET, HEAD' })
      return
    }
    const file = fileFor(pathname)
    if (file === null) {
      sendText(res, 404, 'Not Found')
      return
    }
    const hashed = pathname.startsWith('/assets/')
    if (file !== index && (await serveFile(req, res, file, hashed ? IMMUTABLE : REVALIDATE))) {
      return
    }
    if (hashed) {
      // A missing hashed file (an old page asking for a file from before an upgrade): never answer it with HTML.
      sendText(res, 404, 'Not Found')
      return
    }
    // Any other path is one of the app's own pages: the client-side router reads it.
    const target = new URL(req.url ?? '/', 'http://front')
    if (previews !== null && pathname === '/oembed') {
      await serveOembed(req, res, previews, target)
      return
    }
    if (previews !== null) {
      await servePage(req, res, previews, target)
      return
    }
    if (!(await serveFile(req, res, index, REVALIDATE))) {
      log(`static: ${index} is missing`)
      sendText(res, 500, 'The front has no index.html: was it built?')
    }
  }

  return http.createServer((req, res) => {
    const target = req.url ?? ''
    // Only origin-form targets ("/path?query"): never "http://host/…" or "*".
    if (!target.startsWith('/') || target.startsWith('//')) {
      sendText(res, 400, 'Bad Request')
      return
    }
    const query = target.indexOf('?')
    const pathname = query === -1 ? target : target.slice(0, query)
    if (isProxied(pathname)) {
      forward(req, res)
      return
    }
    serveStatic(req, res, pathname).catch((error: unknown) => {
      log(`static: ${pathname}: ${error instanceof Error ? error.message : String(error)}`)
      if (!res.headersSent) {
        sendText(res, 500, 'Internal Server Error')
      } else {
        res.destroy()
      }
    })
  })
}
