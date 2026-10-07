// @vitest-environment node
// The production front server (server.ts) over real sockets: a temporary dist/ folder, a fake backend, raw HTTP
// requests (node:http, so paths like /../x reach the server as typed, which fetch() would normalise first).
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import http, { type IncomingHttpHeaders, type IncomingMessage, type ServerResponse } from 'node:http'
import type { AddressInfo } from 'node:net'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest'
import { createFrontServer } from './server.ts'

interface Answer {
  status: number
  headers: IncomingHttpHeaders
  body: string
}

interface Seen {
  method: string
  url: string
  headers: IncomingHttpHeaders
  body: string
}

const INDEX = '<!doctype html><title>TonTuyau</title>'
/** Spelled out here on purpose: a change to the policy must change this test too. */
const CSP =
  "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; media-src 'self'; " +
  "connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; " +
  "frame-ancestors 'self'"
const SCRIPT = 'console.log("app")'

let folder: string
let front: http.Server
let frontPort: number
let backend: http.Server
let backendRoute: (req: IncomingMessage, res: ServerResponse) => void
let seen: Seen[] = []
const logged: string[] = []

function listen(server: http.Server): Promise<number> {
  return new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve((server.address() as AddressInfo).port)))
}

function close(server: http.Server): Promise<void> {
  server.closeAllConnections()
  return new Promise((resolve) => server.close(() => resolve()))
}

/** One request to the front, the path sent exactly as given. */
function request(
  target: string,
  options: { method?: string; headers?: Record<string, string>; body?: string; port?: number } = {},
): Promise<Answer> {
  return new Promise((resolve, reject) => {
    const req = http.request(
      {
        host: '127.0.0.1',
        port: options.port ?? frontPort,
        path: target,
        method: options.method ?? 'GET',
        headers: options.headers,
      },
      (res) => {
        const chunks: Buffer[] = []
        res.on('data', (chunk: Buffer) => chunks.push(chunk))
        res.on('end', () =>
          resolve({ status: res.statusCode ?? 0, headers: res.headers, body: Buffer.concat(chunks).toString() }),
        )
        res.on('error', reject)
      },
    )
    req.on('error', reject)
    req.end(options.body)
  })
}

function waitFor(condition: () => boolean, what: string, timeout = 2000): Promise<void> {
  const start = Date.now()
  return new Promise((resolve, reject) => {
    const tick = () => {
      if (condition()) {
        resolve()
      } else if (Date.now() - start > timeout) {
        reject(new Error(`timed out waiting for ${what}`))
      } else {
        setTimeout(tick, 10)
      }
    }
    tick()
  })
}

beforeAll(async () => {
  folder = await mkdtemp(path.join(tmpdir(), 'tontuyau-front-'))
  const dist = path.join(folder, 'dist')
  await mkdir(path.join(dist, 'assets'), { recursive: true })
  await writeFile(path.join(dist, 'index.html'), INDEX)
  await writeFile(path.join(dist, 'assets', 'index-AbC123xy.js'), SCRIPT)
  await writeFile(path.join(dist, 'favicon.svg'), '<svg xmlns="http://www.w3.org/2000/svg"/>')
  await writeFile(path.join(dist, '.hidden'), 'hidden')
  // Next to dist/, never reachable.
  await writeFile(path.join(folder, 'secret.txt'), 'TOP SECRET')

  backend = http.createServer((req, res) => {
    const chunks: Buffer[] = []
    req.on('data', (chunk: Buffer) => chunks.push(chunk))
    req.on('end', () => {
      seen.push({
        method: req.method ?? '',
        url: req.url ?? '',
        headers: req.headers,
        body: Buffer.concat(chunks).toString(),
      })
    })
    backendRoute(req, res)
  })
  const backendPort = await listen(backend)
  front = createFrontServer({
    root: dist,
    backend: new URL(`http://127.0.0.1:${backendPort}`),
    log: (m) => logged.push(m),
  })
  frontPort = await listen(front)
})

afterAll(async () => {
  await close(front)
  await close(backend)
  await rm(folder, { recursive: true, force: true })
})

afterEach(() => {
  seen = []
  logged.length = 0
})

describe('static files', () => {
  it('caches hashed assets for a year, as immutable', async () => {
    const answer = await request('/assets/index-AbC123xy.js')

    expect(answer.status).toBe(200)
    expect(answer.body).toBe(SCRIPT)
    expect(answer.headers['content-type']).toBe('text/javascript; charset=utf-8')
    expect(answer.headers['cache-control']).toBe('public, max-age=31536000, immutable')
    expect(answer.headers['content-length']).toBe(String(SCRIPT.length))
  })

  it('revalidates index.html and the other files, and answers 304 to a matching ETag', async () => {
    for (const target of ['/', '/index.html', '/favicon.svg']) {
      const answer = await request(target)
      expect(answer.status).toBe(200)
      expect(answer.headers['cache-control']).toBe('no-cache')
      expect(answer.headers.etag).toMatch(/^W\/"/)

      const again = await request(target, { headers: { 'if-none-match': answer.headers.etag ?? '' } })
      expect(again.status).toBe(304)
      expect(again.body).toBe('')
    }
    expect((await request('/favicon.svg')).headers['content-type']).toBe('image/svg+xml')
  })

  it('answers HEAD without a body', async () => {
    const answer = await request('/assets/index-AbC123xy.js', { method: 'HEAD' })

    expect(answer.status).toBe(200)
    expect(answer.headers['content-length']).toBe(String(SCRIPT.length))
    expect(answer.body).toBe('')
  })

  it.each([
    '/watch?v=dQw4w9WgXcQ',
    '/channel/UCabcdefghijklmnopqrstuv/shorts',
    '/results?search_query=a%20b',
    '/apiary',
    '/media-x',
  ])('gives index.html to the client-side route %s', async (target) => {
    const answer = await request(target)

    expect(answer.status).toBe(200)
    expect(answer.body).toBe(INDEX)
    expect(answer.headers['content-type']).toBe('text/html; charset=utf-8')
    expect(answer.headers['cache-control']).toBe('no-cache')
    expect(seen).toEqual([])
  })

  it('answers 404, never HTML, for a missing hashed asset', async () => {
    const answer = await request('/assets/index-Old00000.js')

    expect(answer.status).toBe(404)
    expect(answer.headers['content-type']).toBe('text/plain; charset=utf-8')
  })

  it.each([
    '/../secret.txt',
    '/assets/../../secret.txt',
    '/%2e%2e/secret.txt',
    '/assets/%2e%2e/%2e%2e/secret.txt',
    '/..%2fsecret.txt',
    '/%2E%2E%2Fsecret.txt',
    '/assets/..%5c..%5csecret.txt',
    '/.hidden',
    '/%00index.html',
    '/%E0%A4%A',
  ])('never serves anything outside dist/ or hidden: %s', async (target) => {
    const answer = await request(target)

    expect(answer.body).not.toContain('TOP SECRET')
    expect(answer.body).not.toContain('hidden')
    expect([400, 404]).toContain(answer.status)
  })

  it('refuses targets that are not a plain path', async () => {
    expect((await request('//evil.example/x')).status).toBe(400)
    expect((await request('http://evil.example/x')).status).toBe(400)
  })

  it('takes only GET and HEAD for files', async () => {
    const answer = await request('/index.html', { method: 'POST', body: 'x' })

    expect(answer.status).toBe(405)
    expect(answer.headers.allow).toBe('GET, HEAD')
  })

  it('sends the security headers', async () => {
    const answer = await request('/')

    expect(answer.headers['x-content-type-options']).toBe('nosniff')
    expect(answer.headers['referrer-policy']).toBe('same-origin')
    expect(answer.headers['x-frame-options']).toBe('SAMEORIGIN')
  })

  it('sends the Content-Security-Policy with the app, exactly', async () => {
    const answer = await request('/')

    expect(answer.headers['content-type']).toBe('text/html; charset=utf-8')
    expect(answer.headers['content-security-policy']).toBe(CSP)
  })

  it.each([
    ['a page of the app', '/watch/abc', {}, 200],
    ['a hashed asset', '/assets/index-AbC123xy.js', {}, 200],
    ['a revalidated file, 304', '/favicon.svg', { 'if-none-match': '*' }, 304],
    ['a missing asset', '/assets/gone.js', {}, 404],
    ['a refused path', '/%2e%2e/secret.txt', {}, 404],
  ])('sends the Content-Security-Policy on %s too', async (_what, target, headers, status) => {
    const answer = await request(target, { headers })

    expect(answer.status).toBe(status)
    expect(answer.headers['content-security-policy']).toBe(CSP)
  })
})

describe('proxy to the backend', () => {
  it.each(['/api/home?size=2', '/api', '/art/videos/abc/360', '/media/abc?download=1'])(
    'forwards %s as is, with status, headers and body',
    async (target) => {
      backendRoute = (_req, res) => {
        res.writeHead(200, { 'content-type': 'application/json', 'x-tontuyau-version': '1.0.0' })
        res.end('{"ok":true}')
      }

      const answer = await request(target, { headers: { accept: 'application/json', host: 'tontuyau.lan' } })

      expect(answer.status).toBe(200)
      expect(answer.body).toBe('{"ok":true}')
      expect(answer.headers['x-tontuyau-version']).toBe('1.0.0')
      expect(answer.headers['x-content-type-options']).toBe('nosniff')
      expect(answer.headers['content-security-policy']).toBe(CSP)
      expect(seen).toHaveLength(1)
      expect(seen[0]).toMatchObject({ method: 'GET', url: target })
      expect(seen[0]?.headers.accept).toBe('application/json')
      expect(seen[0]?.headers.host).toBe('tontuyau.lan')
      expect(seen[0]?.headers['x-forwarded-host']).toBe('tontuyau.lan')
      expect(seen[0]?.headers['x-forwarded-for']).toBe('127.0.0.1')
    },
  )

  it('forwards Range, If-Range and If-None-Match, and passes 206, 304 and 416 back', async () => {
    backendRoute = (req, res) => {
      if (req.headers['if-none-match'] === '"v1"') {
        res.writeHead(304, { etag: '"v1"' })
        res.end()
      } else if (req.headers.range === 'bytes=999-') {
        res.writeHead(416, { 'content-range': 'bytes */10' })
        res.end()
      } else {
        res.writeHead(206, { 'content-range': 'bytes 2-4/10', 'accept-ranges': 'bytes', 'content-type': 'video/mp4' })
        res.end('234')
      }
    }

    const partial = await request('/media/abc', { headers: { range: 'bytes=2-4', 'if-range': '"v1"' } })
    expect(partial.status).toBe(206)
    expect(partial.body).toBe('234')
    expect(partial.headers['content-range']).toBe('bytes 2-4/10')
    expect(partial.headers['accept-ranges']).toBe('bytes')
    expect(seen[0]?.headers.range).toBe('bytes=2-4')
    expect(seen[0]?.headers['if-range']).toBe('"v1"')

    const notModified = await request('/art/videos/abc/360', { headers: { 'if-none-match': '"v1"' } })
    expect(notModified.status).toBe(304)
    expect(notModified.headers.etag).toBe('"v1"')

    const outOfRange = await request('/media/abc', { headers: { range: 'bytes=999-' } })
    expect(outOfRange.status).toBe(416)
    expect(outOfRange.headers['content-range']).toBe('bytes */10')
  })

  it('streams the answer: the first bytes arrive before the backend has finished', async () => {
    let finish: (() => void) | undefined
    backendRoute = (_req, res) => {
      res.writeHead(200, { 'content-type': 'video/mp4' })
      res.write('first')
      finish = () => res.end('last')
    }

    const firstChunk = await new Promise<string>((resolve, reject) => {
      const req = http.get({ host: '127.0.0.1', port: frontPort, path: '/media/abc' }, (res) => {
        res.once('data', (chunk: Buffer) => {
          resolve(chunk.toString())
          res.resume()
        })
      })
      req.on('error', reject)
    })

    expect(firstChunk).toBe('first')
    expect(finish).toBeDefined()
    finish?.()
  })

  it('streams the request body up and keeps the method', async () => {
    backendRoute = (req, res) => {
      req.resume()
      req.on('end', () => {
        res.writeHead(405, { 'content-type': 'application/problem+json' })
        res.end('{"status":405}')
      })
    }

    const answer = await request('/api/videos', {
      method: 'POST',
      body: 'payload',
      headers: { 'content-type': 'text/plain' },
    })

    expect(answer.status).toBe(405)
    expect(answer.headers['content-type']).toBe('application/problem+json')
    await waitFor(() => seen.length === 1, 'the backend to read the body')
    expect(seen[0]).toMatchObject({ method: 'POST', url: '/api/videos', body: 'payload' })
  })

  it('cancels the upstream request when the browser goes away', async () => {
    let upstreamClosed = false
    backendRoute = (req, res) => {
      res.writeHead(200, { 'content-type': 'video/mp4' })
      res.write('first')
      res.on('close', () => {
        upstreamClosed = true
      })
      req.resume()
    }

    await new Promise<void>((resolve, reject) => {
      const req = http.get({ host: '127.0.0.1', port: frontPort, path: '/media/abc' }, (res) => {
        res.once('data', () => {
          req.destroy()
          resolve()
        })
      })
      req.on('error', () => {
        // destroyed on purpose
      })
      req.setTimeout(2000, () => reject(new Error('no first chunk')))
    })

    await waitFor(() => upstreamClosed, 'the backend to see its request closed')
  })

  it('cancels the upstream request when the browser goes away before the backend answers', async () => {
    let upstreamClosed = false
    let arrived = false
    backendRoute = (req, res) => {
      arrived = true
      req.resume()
      // Never answers (a slow search, a file being opened).
      res.on('close', () => {
        upstreamClosed = true
      })
    }

    const req = http.get({ host: '127.0.0.1', port: frontPort, path: '/api/search?q=slow' })
    req.on('error', () => {
      // destroyed on purpose
    })
    await waitFor(() => arrived, 'the request to reach the backend')
    req.destroy()

    await waitFor(() => upstreamClosed, 'the backend to see its request closed')
  })

  it('does not forward hop-by-hop headers', async () => {
    backendRoute = (_req, res) => {
      res.writeHead(200, { 'content-type': 'text/plain', 'x-backend-only': 'v', connection: 'x-backend-only' })
      res.end('ok')
    }

    const answer = await request('/api/home', {
      headers: { 'x-client-only': '1', connection: 'x-client-only', 'proxy-authorization': 'secret' },
    })

    expect(answer.status).toBe(200)
    expect(seen[0]?.headers['x-client-only']).toBeUndefined()
    expect(seen[0]?.headers['proxy-authorization']).toBeUndefined()
    expect(answer.headers['x-backend-only']).toBeUndefined()
  })

  it('answers a bare 502 (plain text, not ProblemDetails) when the backend is down', async () => {
    const down = createFrontServer({
      root: path.join(folder, 'dist'),
      backend: new URL('http://127.0.0.1:9'),
      log: (m) => logged.push(m),
    })
    const port = await listen(down)
    try {
      const answer = await request('/api/home', { port })

      expect(answer.status).toBe(502)
      expect(answer.headers['content-type']).toBe('text/plain; charset=utf-8')
      expect(answer.body).toContain('Bad Gateway')
      expect(answer.headers['content-security-policy']).toBe(CSP)
      expect(logged.join('\n')).toContain('proxy: GET /api/home')

      // The app itself still loads.
      expect((await request('/', { port })).status).toBe(200)
    } finally {
      await close(down)
    }
  })
})
