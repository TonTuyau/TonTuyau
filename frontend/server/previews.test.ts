// @vitest-environment node
// Link previews (previews.ts) through the front server: a temporary dist/, a fake backend answering the videos and
// channels below, a public address. What a chat app's fetcher would read: the tags in the page, the oEmbed document.
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import http, { type IncomingMessage, type ServerResponse } from 'node:http'
import type { AddressInfo } from 'node:net'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest'
import { escapeHtml, shortened, targetOf } from './previews.ts'
import { createFrontServer } from './server.ts'

const INDEX = '<!doctype html>\n<html>\n  <head>\n    <title>TonTuyau</title>\n  </head>\n  <body></body>\n</html>\n'
const PUBLIC = 'https://tontuyau.example.net'

const video = {
  id: 'vid00000001',
  title: 'I Redesigned "VLC" <After> 25 Years',
  type: 'video',
  thumbnailUrl: '/art/vid00000001.webp',
  thumbnails: [
    { width: 320, url: '/art/vid00000001-320.webp' },
    { width: 1280, url: '/art/vid00000001.webp' },
  ],
  channel: { id: 'UCjuxt', name: 'Juxtopposed' },
  mimeType: 'video/mp4',
  tracks: [
    { kind: 'video', width: 1920, height: 1080 },
    { kind: 'audio', width: null, height: null },
  ],
  mediaUrl: '/media/vid00000001',
  mediaAvailable: true,
}
const short = { ...video, id: 'short000001', type: 'short', tracks: [{ kind: 'video', width: 1080, height: 1920 }] }
const missing = { ...video, id: 'missing0001', mediaAvailable: false }
const channel = {
  id: 'UCjuxt',
  name: 'Juxtopposed',
  description: 'Design videos.\n\nNew ones every week.',
  thumbnailUrl: '/art/UCjuxt-thumb',
  thumbnails: [{ width: 176, url: '/art/UCjuxt-thumb-176' }],
}

let folder: string
let front: http.Server
let frontPort: number
let plain: http.Server
let plainPort: number
let backend: http.Server
let backendRoute: (req: IncomingMessage, res: ServerResponse) => void
const logged: string[] = []

function routes(req: IncomingMessage, res: ServerResponse): void {
  const answers: Record<string, object> = {
    '/api/videos/vid00000001': video,
    '/api/videos/short000001': short,
    '/api/videos/missing0001': missing,
    '/api/channels/UCjuxt': channel,
  }
  const answer = answers[req.url ?? '']
  res.writeHead(answer ? 200 : 404, { 'content-type': 'application/json' })
  res.end(JSON.stringify(answer ?? { title: 'Not found', status: 404 }))
}

function listen(server: http.Server): Promise<number> {
  return new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve((server.address() as AddressInfo).port)))
}

function close(server: http.Server): Promise<void> {
  server.closeAllConnections()
  return new Promise((resolve) => server.close(() => resolve()))
}

async function get(target: string, port = frontPort): Promise<{ status: number; type: string; body: string }> {
  const answer = await fetch(`http://127.0.0.1:${port}${target}`)
  return { status: answer.status, type: answer.headers.get('content-type') ?? '', body: await answer.text() }
}

/** The page's tags: { "og:title": [ … ] }, by property or name. */
async function tags(target: string): Promise<Record<string, string[]>> {
  const { body } = await get(target)
  const found: Record<string, string[]> = {}
  for (const match of body.matchAll(/<meta (?:property|name)="([^"]+)" content="([^"]*)">/g)) {
    const [, key = '', value = ''] = match
    ;(found[key] ??= []).push(value)
  }
  return found
}

beforeAll(async () => {
  folder = await mkdtemp(path.join(tmpdir(), 'tontuyau-previews-'))
  const dist = path.join(folder, 'dist')
  await mkdir(dist, { recursive: true })
  await writeFile(path.join(dist, 'index.html'), INDEX)
  backendRoute = routes
  backend = http.createServer((req, res) => backendRoute(req, res))
  const backendUrl = new URL(`http://127.0.0.1:${await listen(backend)}`)
  front = createFrontServer({
    root: dist,
    backend: backendUrl,
    publicUrl: new URL(PUBLIC),
    log: (m) => logged.push(m),
  })
  frontPort = await listen(front)
  plain = createFrontServer({ root: dist, backend: backendUrl, log: (m) => logged.push(m) })
  plainPort = await listen(plain)
})

afterAll(async () => {
  await close(front)
  await close(plain)
  await close(backend)
  await rm(folder, { recursive: true, force: true })
})

afterEach(() => {
  backendRoute = routes
  logged.length = 0
})

describe('a video’s preview', () => {
  it('names TonTuyau, the title, the thumbnail, and gives the file to play, all at the public address', async () => {
    const found = await tags('/watch?v=vid00000001')

    expect(found).toEqual({
      'og:site_name': ['TonTuyau'],
      'theme-color': ['#0f8b8d'],
      'og:type': ['video.other'],
      'og:url': [`${PUBLIC}/watch?v=vid00000001`],
      'og:title': ['I Redesigned &quot;VLC&quot; &lt;After&gt; 25 Years'],
      'og:image': [`${PUBLIC}/art/vid00000001.webp`],
      'og:image:width': ['1280'],
      'og:video': [`${PUBLIC}/media/vid00000001`],
      'og:video:secure_url': [`${PUBLIC}/media/vid00000001`],
      'og:video:type': ['video/mp4'],
      'og:video:width': ['1920'],
      'og:video:height': ['1080'],
      'twitter:card': ['player'],
      'twitter:player:stream': [`${PUBLIC}/media/vid00000001`],
      'twitter:player:stream:content_type': ['video/mp4'],
      'twitter:player:width': ['1920'],
      'twitter:player:height': ['1080'],
    })
  })

  it('links the oEmbed document that names the channel, which the page’s <head> still ends after', async () => {
    const { status, type, body } = await get('/watch?v=vid00000001')

    expect(status).toBe(200)
    expect(type).toBe('text/html; charset=utf-8')
    expect(body).toContain(
      `<link rel="alternate" type="application/json+oembed" href="${PUBLIC}/oembed?v=vid00000001">`,
    )
    expect(body.indexOf('og:title')).toBeLessThan(body.indexOf('</head>'))
    expect(body).toContain('<title>TonTuyau</title>')
  })

  it('has a short’s own address and its portrait size', async () => {
    const found = await tags('/shorts/short000001')

    expect(found['og:url']).toEqual([`${PUBLIC}/shorts/short000001`])
    expect(found['og:video:width']).toEqual(['1080'])
    expect(found['og:video:height']).toEqual(['1920'])
  })

  it('has no player when the file is missing: the thumbnail, large', async () => {
    const found = await tags('/watch?v=missing0001')

    expect(found['og:video']).toBeUndefined()
    expect(found['twitter:player:stream']).toBeUndefined()
    expect(found['twitter:card']).toEqual(['summary_large_image'])
    expect(found['og:image']).toEqual([`${PUBLIC}/art/vid00000001.webp`])
  })
})

describe('the oEmbed document', () => {
  it('gives the channel as the author, linked to its page, and TonTuyau as the provider', async () => {
    const { status, type, body } = await get('/oembed?v=vid00000001')

    expect(status).toBe(200)
    expect(type).toBe('application/json; charset=utf-8')
    expect(JSON.parse(body)).toEqual({
      version: '1.0',
      type: 'link',
      title: video.title,
      author_name: 'Juxtopposed',
      author_url: `${PUBLIC}/channel/UCjuxt`,
      provider_name: 'TonTuyau',
      provider_url: `${PUBLIC}/`,
    })
  })

  it('is not found for an unknown video or without one', async () => {
    expect((await get('/oembed?v=nope')).status).toBe(404)
    expect((await get('/oembed')).status).toBe(404)
  })
})

describe('a channel’s preview', () => {
  it('names TonTuyau, the channel, its description on one line and its icon, without a player', async () => {
    const found = await tags('/channel/UCjuxt/shorts')

    expect(found).toEqual({
      'og:site_name': ['TonTuyau'],
      'theme-color': ['#0f8b8d'],
      'og:type': ['website'],
      'og:url': [`${PUBLIC}/channel/UCjuxt`],
      'og:title': ['Juxtopposed'],
      'og:description': ['Design videos. New ones every week.'],
      'og:image': [`${PUBLIC}/art/UCjuxt-thumb-176`],
      'twitter:card': ['summary'],
    })
  })
})

describe('any other page', () => {
  it.each(['/', '/channels', '/results?search_query=vlc', '/watch', '/watch?v=nope', '/channel/UCnope'])(
    '%s: TonTuyau alone',
    async (target) => {
      const found = await tags(target)

      expect(found).toEqual({
        'og:site_name': ['TonTuyau'],
        'theme-color': ['#0f8b8d'],
        'og:type': ['website'],
        'og:url': [`${PUBLIC}/`],
        'og:title': ['TonTuyau'],
      })
    },
  )

  it('is still served when the backend is down, the failure logged', async () => {
    backendRoute = (req) => req.socket.destroy()

    const { status, body } = await get('/watch?v=vid00000001')

    expect(status).toBe(200)
    expect(body).toContain('<meta property="og:title" content="TonTuyau">')
    expect(logged).toEqual([expect.stringMatching(/^preview: GET \/api\/videos\/vid00000001: /)])
  })
})

describe('without a public address', () => {
  it('serves index.html as built, and no oEmbed', async () => {
    expect((await get('/watch?v=vid00000001', plainPort)).body).toBe(INDEX)
    expect((await get('/oembed?v=vid00000001', plainPort)).body).toBe(INDEX)
  })
})

describe('the pieces', () => {
  it('finds the video or channel a path names', () => {
    const none = new URLSearchParams()
    expect(targetOf('/watch', new URLSearchParams('v=abc'))).toEqual({ kind: 'video', id: 'abc' })
    expect(targetOf('/shorts/abc', none)).toEqual({ kind: 'video', id: 'abc' })
    expect(targetOf('/channel/UC%2Fx/streams', none)).toEqual({ kind: 'channel', id: 'UC/x' })
    expect(targetOf('/channel/%E0%A4%A', none)).toEqual({ kind: 'site' })
    expect(targetOf('/shorts/abc/more', none)).toEqual({ kind: 'site' })
    expect(targetOf('/watch', none)).toEqual({ kind: 'site' })
  })

  it('escapes what could end an attribute or start markup', () => {
    expect(escapeHtml(`a&b<c>d"e'f`)).toBe('a&amp;b&lt;c&gt;d&quot;e&#39;f')
  })

  it('cuts a long description after a whole word', () => {
    const long = `${'word '.repeat(70)}end`

    expect(shortened(long)).toHaveLength(300)
    expect(shortened(long)).toMatch(/word…$/)
    expect(shortened('  two\n\nlines  ')).toBe('two lines')
  })
})
