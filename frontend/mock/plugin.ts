// Vite plugin for `pnpm run dev:mockup`: the dev server itself answers /api, /art and /media from the
// fixtures, so the app runs unchanged (same fetch calls as against the real backend).
// Review knobs: open /__mock in the browser (scenario normal / empty / error / down / offline, response delay, logged
// in as admin or not: the admin pages, mock/admin.ts).
import { createReadStream, statSync } from 'node:fs'
import type { IncomingMessage, ServerResponse } from 'node:http'
import { fileURLToPath } from 'node:url'
import type { Connect, Plugin } from 'vite'
import { directory, logInAs, sessionAccount } from './accounts.ts'
import { adminMockState, handleAdmin, seedQueue, type AdminMockState } from './admin.ts'
import { artFor } from './art.ts'
import { emptyDb, fixturesDir, loadDb, type MockDb, type VideoFixture } from './db.ts'
import { handleApi, notFound, problemResponse, type MockResponse } from './handler.ts'
import { servedWidth } from './imageSizes.ts'
import { hasRealLayer, loadRealDb, realChannelArt, realDir } from './realChannels.ts'

type Scenario = 'normal' | 'empty' | 'error' | 'down' | 'offline'
const scenarios: readonly Scenario[] = ['normal', 'empty', 'error', 'down', 'offline']
type ChannelsMode = 'real' | 'synthetic'

interface State {
  db: MockDb
  scenario: Scenario
  delayMs: number
  /** 'real' only when the local real-channels layer exists (dev-data/mockup, git-ignored). */
  channels: ChannelsMode
  realAvailable: boolean
  /** The admin side: logged in or not, the runs, keys and settings (in memory). */
  admin: AdminMockState
}

/** (Re)builds the library for the current channels mode. */
function buildDb(state: Pick<State, 'channels' | 'realAvailable'>): MockDb {
  const synthetic = loadDb()
  return state.channels === 'real' && state.realAvailable ? loadRealDb(synthetic) : synthetic
}

function describeMode(state: State): string {
  return state.channels === 'real'
    ? `real channels from ${realDir} (${state.db.channels.length} channels, ${state.db.videos.length} synthetic videos)`
    : `synthetic channels (${state.db.channels.length} channels, ${state.db.videos.length} videos)`
}

const samples = {
  landscape: fileURLToPath(new URL('./media/sample-landscape.mp4', import.meta.url)),
  portrait: fileURLToPath(new URL('./media/sample-portrait.mp4', import.meta.url)),
  fourByThree: fileURLToPath(new URL('./media/sample-4x3.mp4', import.meta.url)),
  wide: fileURLToPath(new URL('./media/sample-wide.mp4', import.meta.url)),
  // 3 minutes with a running clock, for the demo video and its chapters (2.0, C1: the player's bar).
  chapters: fileURLToPath(new URL('./media/sample-chapters.mp4', import.meta.url)),
}

/**
 * The test pattern with the video's shape (its video track, as the API gives it), so the player's box can be seen
 * fitting it. No video track: the 4:3 pattern, so the shape read from the loaded file differs from the 16:9 start.
 */
export function sampleFor(video: VideoFixture, demo = false): keyof typeof samples {
  if (demo) return 'chapters'
  const track = video.tracks.find((candidate) => candidate.kind === 'video')
  if (!track?.width || !track.height) return video.type === 'short' ? 'portrait' : 'fourByThree'
  const ratio = track.width / track.height
  if (ratio < 1) return 'portrait'
  if (ratio < 1.5) return 'fourByThree'
  return ratio > 2 ? 'wide' : 'landscape'
}

/** Ids starting with "mockGone": the API says the file is there, the share doesn't have it (edge case, M5). */
export const missingOnShare = (id: string) => id.startsWith('mockGone')

/** A request's JSON body; undefined when there's none or it isn't JSON. */
function readJson(req: IncomingMessage): Promise<unknown> {
  return new Promise((resolve) => {
    const chunks: Buffer[] = []
    req.on('data', (chunk: Buffer) => chunks.push(chunk))
    req.on('end', () => {
      try {
        resolve(chunks.length > 0 ? JSON.parse(Buffer.concat(chunks).toString()) : undefined)
      } catch {
        resolve(undefined)
      }
    })
    req.on('error', () => resolve(undefined))
  })
}

function send(res: ServerResponse, response: MockResponse, head = false): void {
  res.statusCode = response.status
  for (const [name, value] of Object.entries(response.headers)) {
    res.setHeader(name, value)
  }
  res.end(head ? undefined : response.body)
}

/** RFC 9110 single range: "bytes=a-b", "bytes=a-", "bytes=-n". Null = whole file; 'invalid' = 416. */
function parseRange(header: string | undefined, size: number): { start: number; end: number } | null | 'invalid' {
  if (!header) return null
  const match = /^bytes=(\d*)-(\d*)$/.exec(header.trim())
  if (!match) return 'invalid'
  const [, from = '', to = ''] = match
  if (from === '' && to === '') return 'invalid'
  const start = from === '' ? Math.max(0, size - Number(to)) : Number(from)
  const end = from === '' || to === '' ? size - 1 : Math.min(Number(to), size - 1)
  return start > end || start >= size ? 'invalid' : { start, end }
}

/** Content-Disposition with the video's title as the file name (Q5-3): forbidden characters replaced. */
function attachment(title: string): string {
  const name = `${title.replace(/[\\/:*?"<>|\p{Cc}]/gu, '_').trim() || 'video'}.mp4`
  const ascii = name.replace(/[^\x20-\x7e]/g, '_')
  return `attachment; filename="${ascii}"; filename*=UTF-8''${encodeURIComponent(name)}`
}

function serveMedia(state: State, req: IncomingMessage, res: ServerResponse, path: string): void {
  const [, , rawId, download, ...rest] = path.split('/')
  const id = decodeURIComponent(rawId ?? '')
  const video = state.db.videos.find((candidate) => candidate.id === id)
  if (
    !video ||
    !video.mediaAvailable ||
    missingOnShare(id) ||
    rest.length > 0 ||
    (download !== undefined && download !== 'download')
  ) {
    send(res, problemResponse(notFound(`No media file for ${id}.`)))
    return
  }
  const file = samples[sampleFor(video, video.id === state.db.videos[0]?.id)]
  const size = statSync(file).size
  const range = parseRange(req.headers.range, size)
  res.setHeader('accept-ranges', 'bytes')
  res.setHeader('content-type', 'video/mp4')
  if (download === 'download') {
    res.setHeader('content-disposition', attachment(video.title))
  }
  if (range === 'invalid') {
    res.statusCode = 416
    res.setHeader('content-range', `bytes */${size}`)
    res.end()
    return
  }
  const { start, end } = range ?? { start: 0, end: size - 1 }
  res.statusCode = range ? 206 : 200
  res.setHeader('content-length', String(end - start + 1))
  if (range) {
    res.setHeader('content-range', `bytes ${start}-${end}/${size}`)
  }
  if (req.method === 'HEAD') {
    res.end()
    return
  }
  createReadStream(file, { start, end }).pipe(res)
}

function controlPage(state: State, url: URL): MockResponse {
  const scenario = url.searchParams.get('scenario')
  const known = scenarios.find((value) => value === scenario)
  if (known) {
    state.scenario = known
  }
  const channels = url.searchParams.get('channels')
  if ((channels === 'real' && state.realAvailable) || channels === 'synthetic') {
    if (channels !== state.channels) {
      state.channels = channels
      state.db = buildDb(state)
      console.log(`[mock] channels: ${describeMode(state)}`)
    }
  }
  const admin = url.searchParams.get('admin')
  if (admin === 'on' || admin === 'off') {
    logInAs(state.admin.accounts, admin === 'on' ? 'admin' : null)
  }
  // 2.3.0: a user's session; a library for accounts only.
  if (url.searchParams.get('user') === 'on') {
    logInAs(state.admin.accounts, 'user')
  }
  const ldap = url.searchParams.get('ldap')
  if (ldap === 'up' || ldap === 'down') {
    state.admin.accounts.ldapUp = ldap === 'up'
  }
  const only = url.searchParams.get('private')
  if (only === 'on' || only === 'off') {
    state.admin.accounts.access = { ...state.admin.accounts.access, accountsOnly: only === 'on' }
  }
  const disk = url.searchParams.get('disk')
  if (disk === 'normal' || disk === 'low' || disk === 'unknown') {
    state.admin.disk = disk
  }
  const queue = url.searchParams.get('queue')
  if (queue === 'youTube' || queue === 'hand' || queue === 'none') {
    seedQueue(state.admin, queue)
  }
  const delay = url.searchParams.get('delay')
  if (delay !== null && /^\d{1,5}$/.test(delay)) {
    state.delayMs = Number(delay)
  }
  const link = (query: string, label: string) => `<a href="/__mock?${query}">${label}</a>`
  const body = `<!doctype html><meta charset="utf-8"><title>TonTuyau mock</title>
<style>body{font:16px system-ui;margin:2rem;line-height:1.8}a{margin-right:1rem}</style>
<h1>TonTuyau mock API</h1>
<p>Scenario: <b>${state.scenario}</b> · delay: <b>${state.delayMs} ms</b> · ${describeMode(state)}</p>
<p>Channels: ${
    state.realAvailable
      ? `${link('channels=real', 'real channels (local, not committed)')} ${link('channels=synthetic', 'synthetic channels')}`
      : `synthetic only (no real-channels layer in ${realDir})`
  }</p>
<p>Scenario: ${link('scenario=normal', 'normal')} ${link('scenario=empty', 'empty library')} ${link('scenario=error', 'every /api call fails (500)')}
${link('scenario=down', 'backend down behind the proxy (bare 502)')} ${link('scenario=offline', 'no answer at all (connection dropped)')}</p>
<p>Account: <b>${describeSession(state)}</b> ${link('admin=on', 'log in as admin')} ${link('user=on', 'log in as a user')} ${link('admin=off', 'log out')} (then <a href="/admin">the admin pages</a>, <a href="/account">My account</a>)</p>
<p>Log in yourself with a local account: admin or camille (the password is the name).</p>
<p>LDAP directory (2.3.0): <b>${state.admin.accounts.ldapUp ? 'answering' : 'down'}</b> ${link('ldap=up', 'answering')} ${link('ldap=down', 'down')}<br>
Its people (password = the name): ${directory.map((person) => `${person.uid} (${person.groups.map((group) => group.split(',')[0]).join(', ') || 'no group'})`).join(', ')}. camille clashes: a local account has that name.</p>
<p>Who can watch (2.3.0): <b>${state.admin.accounts.access.accountsOnly ? 'logged-in accounts only' : 'everyone'}</b> ${link('private=on', 'accounts only')} ${link('private=off', 'everyone')}</p>
<p>Disk (2.2.0, the dashboard's storage): <b>${state.admin.disk}</b> ${link('disk=normal', 'roomy')} ${link('disk=low', 'almost full')} ${link('disk=unknown', 'not saying its free space')}</p>
<p>Tasks page (2.3.0, Run now): ${link('queue=youTube', 'busy, a sync YouTube refused holds the queue')} ${link('queue=hand', 'busy, a sync held by hand')} ${link('queue=none', 'busy, nothing held')}</p>
<p>Delay: ${link('delay=0', '0')} ${link('delay=300', '300 ms')} ${link('delay=1500', '1.5 s')} ${link('delay=5000', '5 s')}</p>
<p><a href="/">← back to the app</a></p>`
  return { status: 200, headers: { 'content-type': 'text/html; charset=utf-8' }, body }
}

function describeSession(state: State): string {
  const account = sessionAccount(state.admin.accounts)
  return account === null ? 'logged out' : `${account.username} (${account.role}, ${account.source})`
}

/** 2.3.0: a library for accounts only answers 401 to anyone else, the pictures and the files too (AccountsOnlyGate). */
function closed(state: State, path: string): boolean {
  const open = ['/api/auth/', '/api/admin/', '/api/extension/']
  return (
    state.admin.accounts.access.accountsOnly &&
    sessionAccount(state.admin.accounts) === null &&
    ((path.startsWith('/api/') && !open.some((prefix) => path.startsWith(prefix))) ||
      path.startsWith('/art/') ||
      path.startsWith('/media/'))
  )
}

export function createMockMiddleware(state: State): Connect.NextHandleFunction {
  return (req, res, next) => {
    const url = new URL(req.url ?? '/', 'http://mock.local')
    const path = url.pathname
    const head = req.method === 'HEAD'
    try {
      if (path === '/__mock') {
        send(res, controlPage(state, url))
      } else if (closed(state, path)) {
        const detail = 'This TonTuyau is for its accounts: log in to watch.'
        send(res, problemResponse({ title: 'Not logged in', status: 401, detail }), head)
      } else if (path.startsWith('/api/') && state.scenario === 'offline') {
        // What a dead server or a lost network looks like to fetch(): the connection just ends.
        setTimeout(() => req.socket.destroy(), state.delayMs)
      } else if (path.startsWith('/api/') && state.scenario === 'down') {
        // What Vite's proxy answers when the backend isn't running: 502, text/plain, empty.
        setTimeout(
          () => send(res, { status: 502, headers: { 'content-type': 'text/plain' }, body: '' }, head),
          state.delayMs,
        )
      } else if ((path.startsWith('/api/admin/') || path.startsWith('/api/auth/')) && state.scenario !== 'error') {
        void readJson(req).then((body) => {
          const db = state.scenario === 'empty' ? emptyDb : state.db
          const answer = handleAdmin(db, state.admin, req.method ?? 'GET', url, body)
          const response: MockResponse =
            answer === null || answer.body === undefined
              ? { status: answer?.status ?? 404, headers: {}, body: '' }
              : {
                  status: answer.status,
                  headers: {
                    'content-type':
                      answer.status >= 400 ? 'application/problem+json' : 'application/json; charset=utf-8',
                  },
                  body: JSON.stringify(answer.body),
                }
          setTimeout(() => send(res, response, head), state.delayMs)
        })
      } else if (path.startsWith('/api/')) {
        const db = state.scenario === 'empty' ? emptyDb : state.db
        const response =
          state.scenario === 'error'
            ? problemResponse({
                type: 'https://tontuyau/errors/internal',
                title: 'Internal error',
                status: 500,
                detail: 'Mock scenario "error": every API call fails. Reset it at /__mock.',
              })
            : handleApi(db, req.method ?? 'GET', url)
        setTimeout(() => send(res, response, head), state.delayMs)
      } else if (path.startsWith('/art/channels/') && state.db.channelArtDir) {
        const [, , , id = '', kind = '', width, ...rest] = path.split('/').map((part) => decodeURIComponent(part))
        // A resized version (v1.4): the real layer only has the originals, served for every listed width.
        const sized =
          width === undefined || ((kind === 'thumb' || kind === 'banner') && servedWidth(kind, width) !== null)
        const file = sized && rest.length === 0 ? realChannelArt(state.db, id, kind) : null
        if (file) {
          res.setHeader('content-type', 'image/jpeg')
          res.setHeader('cache-control', 'no-cache')
          res.setHeader('content-length', String(statSync(file).size))
          if (head) res.end()
          else createReadStream(file).pipe(res)
        } else {
          send(res, problemResponse(notFound(`No image at ${path}.`)), head)
        }
      } else if (path.startsWith('/art/')) {
        const image = artFor(state.db, path)
        send(
          res,
          image === null
            ? problemResponse(notFound(`No image at ${path}.`))
            : { status: 200, headers: { 'content-type': 'image/svg+xml', 'cache-control': 'no-cache' }, body: image },
          head,
        )
      } else if (path.startsWith('/media/')) {
        serveMedia(state, req, res, path)
      } else {
        next()
      }
    } catch (error) {
      console.error('[mock]', error)
      send(
        res,
        problemResponse({ title: 'Internal error', status: 500, detail: 'The mock API failed (see the terminal).' }),
      )
    }
  }
}

export function mockApi(): Plugin {
  const realAvailable = hasRealLayer()
  const mode: ChannelsMode = realAvailable ? 'real' : 'synthetic'
  const db = buildDb({ channels: mode, realAvailable })
  const state: State = {
    db,
    scenario: 'normal',
    delayMs: 300,
    channels: mode,
    realAvailable,
    admin: adminMockState(db),
  }
  console.log(`[mock] channels: ${describeMode(state)}`)
  const reload = (file: string) => {
    if (file.startsWith(fixturesDir)) {
      try {
        state.db = buildDb(state)
        console.log(`[mock] fixtures reloaded: ${describeMode(state)}`)
      } catch (error) {
        console.error('[mock] fixtures not reloaded (invalid JSON?)', error)
      }
    }
  }
  return {
    name: 'tontuyau-mock-api',
    configureServer(server) {
      server.watcher.add(fixturesDir)
      server.watcher.on('change', reload)
      server.middlewares.use(createMockMiddleware(state))
    },
    configurePreviewServer(server) {
      server.middlewares.use(createMockMiddleware(state))
    },
  }
}
